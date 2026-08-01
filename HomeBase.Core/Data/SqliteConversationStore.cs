using System;
using System.IO;
using Microsoft.Data.Sqlite;

namespace HomeBase.Core.Data;

public sealed class SqliteConversationStore : IConversationStore
{
    private readonly string _connectionString;
    private readonly Lock _lock = new();

    public SqliteConversationStore(string? databasePath = null)
    {
        var path = databasePath ?? GetDefaultDatabasePath();
        var directory = Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException("Database path must include a directory.");
        Directory.CreateDirectory(directory);

        _connectionString = $"Data Source={path}";
        EnsureSchemaCreated();
    }

    public void RecordUserMessage(string conversationId, string content)
    {
        lock (_lock)
        {
            using var connection = OpenConnection();
            using var transaction = connection.BeginTransaction();

            Execute(connection, transaction,
                "INSERT OR IGNORE INTO Conversations (Id, CreatedAtUtc) VALUES ($id, $createdAt);",
                ("$id", conversationId), ("$createdAt", UtcNow()));

            Execute(connection, transaction,
                """
                INSERT INTO Messages (Id, ConversationId, Role, Content, Status, CreatedAtUtc)
                VALUES ($id, $conversationId, 'user', $content, 'Completed', $createdAt);
                """,
                ("$id", Guid.NewGuid().ToString("N")),
                ("$conversationId", conversationId),
                ("$content", content),
                ("$createdAt", UtcNow()));

            transaction.Commit();
        }
    }

    public void BeginAssistantMessage(string conversationId, string messageId)
    {
        lock (_lock)
        {
            using var connection = OpenConnection();
            Execute(connection, null,
                """
                INSERT INTO Messages (Id, ConversationId, Role, Content, Status, CreatedAtUtc)
                VALUES ($id, $conversationId, 'assistant', '', 'Pending', $createdAt);
                """,
                ("$id", messageId),
                ("$conversationId", conversationId),
                ("$createdAt", UtcNow()));
        }
    }

    public void MarkCompleted(string messageId, string content) => UpdateStatus(messageId, content, "Completed");

    public void MarkIncomplete(string messageId, string content) => UpdateStatus(messageId, content, "Incomplete");

    public void MarkFailed(string messageId, string content) => UpdateStatus(messageId, content, "Failed");

    public StoredMessage? GetMessage(string messageId)
    {
        lock (_lock)
        {
            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT Id, ConversationId, Role, Content, Status FROM Messages WHERE Id = $id;";
            command.Parameters.AddWithValue("$id", messageId);

            using var reader = command.ExecuteReader();
            if (!reader.Read())
            {
                return null;
            }

            return new StoredMessage(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4));
        }
    }

    private void UpdateStatus(string messageId, string content, string status)
    {
        lock (_lock)
        {
            using var connection = OpenConnection();
            Execute(connection, null,
                "UPDATE Messages SET Content = $content, Status = $status, CompletedAtUtc = $completedAt WHERE Id = $id;",
                ("$content", content), ("$status", status), ("$completedAt", UtcNow()), ("$id", messageId));
        }
    }

    private void EnsureSchemaCreated()
    {
        using var connection = OpenConnection();
        Execute(connection, null,
            """
            CREATE TABLE IF NOT EXISTS Conversations (
                Id TEXT PRIMARY KEY,
                CreatedAtUtc TEXT NOT NULL
            );
            """);

        Execute(connection, null,
            """
            CREATE TABLE IF NOT EXISTS Messages (
                Id TEXT PRIMARY KEY,
                ConversationId TEXT NOT NULL REFERENCES Conversations(Id),
                Role TEXT NOT NULL,
                Content TEXT NOT NULL,
                Status TEXT NOT NULL,
                CreatedAtUtc TEXT NOT NULL,
                CompletedAtUtc TEXT NULL
            );
            """);
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private static void Execute(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        string commandText,
        params (string Name, object Value)[] parameters)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = commandText;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        command.ExecuteNonQuery();
    }

    private static string UtcNow() => DateTime.UtcNow.ToString("O");

    private static string GetDefaultDatabasePath()
    {
        var dataDirectory = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        if (string.IsNullOrWhiteSpace(dataDirectory))
        {
            dataDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
        }

        return Path.Combine(dataDirectory, "HomeBase", "homebase.db");
    }
}

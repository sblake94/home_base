using HomeBase.Core.Documents;
using HomeBase.SharedLib.Logging;
using Moq;

namespace HomeBase.Core.Tests.Documents;

public class FileDocumentServiceTests : IDisposable
{
	private readonly string _tempDirectory;

	public FileDocumentServiceTests()
	{
		_tempDirectory = Path.Combine(Path.GetTempPath(), "homebase-document-tests-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(_tempDirectory);
	}

	public void Dispose()
	{
		if (Directory.Exists(_tempDirectory))
		{
			Directory.Delete(_tempDirectory, recursive: true);
		}
	}

	private FileDocumentService CreateService() => new(_tempDirectory, new Mock<ICustomLoggerFactory>().Object);

	[Fact]
	public async Task WritesAndReadsDocumentContent()
	{
		var service = CreateService();
		var path = Path.Combine(_tempDirectory, "document.txt");
		const string content = "First line\nSecond line";

		await service.WriteAsync(path, content);

		var actual = await service.ReadAsync(path);

		Assert.Equal(content, actual);
	}

	[Fact]
	public async Task ThrowsWhenReadingMissingFile()
	{
		var service = CreateService();
		var path = Path.Combine(_tempDirectory, "missing.txt");

		var exception = await Assert.ThrowsAsync<FileNotFoundException>(() => service.ReadAsync(path));

		Assert.Equal(path, exception.FileName);
	}

	[Fact]
	public async Task WritesNewFile()
	{
		var service = CreateService();
		var path = Path.Combine(_tempDirectory, "new-document.txt");
		const string content = "New document";

		await service.WriteAsync(path, content);

		Assert.True(File.Exists(path));
		Assert.Equal(content, await File.ReadAllTextAsync(path));
	}

	[Fact]
	public async Task RejectsRelativeTraversalOutsideWorkspace()
	{
		var service = CreateService();
		var path = Path.Combine(_tempDirectory, "..", "outside.txt");

		try
		{
			await service.WriteAsync(path, "content");
			Assert.Fail("Expected DocumentServiceException was not thrown.");
		}
		catch (DocumentServiceException ex)
		{
			Assert.Equal("Path is outside the document workspace.", ex.Message);
		}
	}

	[Fact]
	public async Task RejectsAbsolutePathOutsideWorkspace()
	{
		var service = CreateService();
		var outsideDirectory = _tempDirectory + "-outside";
		var path = Path.Combine(outsideDirectory, "outside.txt");

		try
		{
			await service.WriteAsync(path, "content");
			Assert.Fail("Expected DocumentServiceException was not thrown.");
		}
		catch (DocumentServiceException ex)
		{
			Assert.Equal("Path is outside the document workspace.", ex.Message);
		}
	}

	[Fact]
	public async Task RejectsPathWithSimilarDirectoryPrefix()
	{
		var service = CreateService();
		var path = Path.Combine(_tempDirectory + "-other", "outside.txt");

		try
		{
			await service.WriteAsync(path, "content");
			Assert.Fail("Expected DocumentServiceException was not thrown.");
		}
		catch (DocumentServiceException ex)
		{
			Assert.Equal("Path is outside the document workspace.", ex.Message);
		}
	}

    	
 	[Fact]
 	public async Task RejectsEmptyPath()
 	{
 		var service = CreateService();
 		try
		{ 
			await service.WriteAsync(string.Empty, "content");
			Assert.Fail("Expected DocumentServiceException was not thrown.");
		}
		catch (DocumentServiceException exception)
		{
			Assert.Equal(FileDocumentService.InvalidPathErrorCode, exception.ErrorCode);
		}
 	}
}

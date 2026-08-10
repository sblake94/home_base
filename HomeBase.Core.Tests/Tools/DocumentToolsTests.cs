using Xunit;
using System.Collections.Generic;
using Moq;
using HomeBase.Core.Documents;
using HomeBase.Core.Tools;

namespace HomeBase.Core.Tests.Tools;
public class DocumentToolsTests
{
    [Fact]
    public void ListDocumentNames_ReturnsEmptyList_WhenDocumentServiceIsNull()
    {
        // Arrange
        DocumentTools.DocumentService = null;

        // Act & assert
        var result = DocumentTools.ListDocumentNames();
        Assert.IsType<List<string>>(result);
        var documentNames = (List<string>)result;
        Assert.Empty(documentNames);
    }

    [Fact]
    public void ListDocumentNames_ReturnsDocumentNames_WhenDocumentServiceIsInitialized()
    {
        // Arrange
        var mockDocumentService = new Mock<IDocumentService>();
        mockDocumentService.Setup(ds => ds.ListDocuments()).Returns(new List<string> { "doc1", "doc2" });
        DocumentTools.DocumentService = mockDocumentService.Object;

        // Act
        var result = DocumentTools.ListDocumentNames();

        // Assert
        Assert.IsType<List<string>>(result);
        var documentNames = (List<string>)result;
        Assert.Equal(2, documentNames.Count);
        Assert.Contains("doc1", documentNames);
        Assert.Contains("doc2", documentNames);
    }

    [Fact]
    public void ReadDocument_ReturnsEmptyString_WhenDocumentServiceIsNull()
    {
        // Arrange
        DocumentTools.DocumentService = null;

        // Act
        var result = DocumentTools.ReadDocument("test-document");

        // Assert
        Assert.IsType<string>(result);
        Assert.Equal(string.Empty, result);
    }


    [Fact]        
    public void ReadDocument_ReturnsDocumentContent_WhenDocumentServiceIsInitialized()
    {
        // Arrange
        const string documentName = "test-document";
        const string documentContent = "This is the content of the test document.";
        var mockDocumentService = new Mock<IDocumentService>();
        mockDocumentService.Setup(ds => ds.ReadAsync(documentName)).ReturnsAsync(documentContent);
        DocumentTools.DocumentService = mockDocumentService.Object;

        // Act
        var result = DocumentTools.ReadDocument(documentName);

        // Assert
        Assert.IsType<string>(result);
        Assert.Equal(documentContent, result);
    }
}
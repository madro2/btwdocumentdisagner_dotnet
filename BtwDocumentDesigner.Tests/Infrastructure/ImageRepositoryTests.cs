using System;
using System.IO;
using System.Threading.Tasks;
using BtwDocumentDesigner.Domain;
using BtwDocumentDesigner.Infrastructure;
using BtwDocumentDesigner.Infrastructure.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace BtwDocumentDesigner.Tests.Infrastructure;

public sealed class ImageRepositoryTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Mock<IWebHostEnvironment> _environment;
    private readonly string _tempDirectory;
    private readonly ImageRepository _repository;

    public ImageRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);

        _tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDirectory);

        _environment = new Mock<IWebHostEnvironment>();
        _environment.Setup(e => e.ContentRootPath).Returns(_tempDirectory);

        _repository = new ImageRepository(_db, _environment.Object);
    }

    [Fact]
    public void Constructor_CreatesUploadDirectory_WhenItDoesNotExist()
    {
        var expectedPath = Path.Combine(_tempDirectory, "wwwroot", "uploads");
        Assert.True(Directory.Exists(expectedPath));
    }

    [Fact]
    public async Task AddAsync_SavesFileAndClearsImageDataBeforeDbInsert()
    {
        var id = Guid.NewGuid();
        var image = new PdfDesignImage
        {
            Id = id,
            FileName = "test.png",
            ImageData = new byte[] { 1, 2, 3 },
            ContentType = "image/png",
            UploadDate = DateTime.UtcNow
        };

        await _repository.AddAsync(image);

        var expectedPath = Path.Combine(_tempDirectory, "wwwroot", "uploads", $"{id}.png");
        Assert.True(File.Exists(expectedPath));
        
        var fileBytes = await File.ReadAllBytesAsync(expectedPath);
        Assert.Equal(new byte[] { 1, 2, 3 }, fileBytes);

        var dbImage = await _db.PdfDesignImages.FindAsync(id);
        Assert.NotNull(dbImage);
        Assert.Empty(dbImage.ImageData);
    }

    [Fact]
    public async Task GetByIdAsync_PopulatesImageDataFromFile_WhenDbHasEmptyData()
    {
        var id = Guid.NewGuid();
        var expectedPath = Path.Combine(_tempDirectory, "wwwroot", "uploads", $"{id}.png");
        
        var uploadFolder = Path.Combine(_tempDirectory, "wwwroot", "uploads");
        Directory.CreateDirectory(uploadFolder);
        await File.WriteAllBytesAsync(expectedPath, new byte[] { 4, 5, 6 });

        var image = new PdfDesignImage
        {
            Id = id,
            FileName = "test.png",
            ImageData = Array.Empty<byte>(),
            ContentType = "image/png",
            UploadDate = DateTime.UtcNow
        };
        _db.PdfDesignImages.Add(image);
        await _db.SaveChangesAsync();

        var retrievedImage = await _repository.GetByIdAsync(id);

        Assert.NotNull(retrievedImage);
        Assert.Equal(new byte[] { 4, 5, 6 }, retrievedImage.ImageData);
    }

    [Fact]
    public async Task DeleteAsync_RemovesFileAndDbEntry()
    {
        var id = Guid.NewGuid();
        var uploadFolder = Path.Combine(_tempDirectory, "wwwroot", "uploads");
        Directory.CreateDirectory(uploadFolder);
        var expectedPath = Path.Combine(uploadFolder, $"{id}.png");
        await File.WriteAllBytesAsync(expectedPath, new byte[] { 7, 8, 9 });

        var image = new PdfDesignImage
        {
            Id = id,
            FileName = "test.png",
            ImageData = Array.Empty<byte>(),
            ContentType = "image/png",
            UploadDate = DateTime.UtcNow
        };
        _db.PdfDesignImages.Add(image);
        await _db.SaveChangesAsync();

        await _repository.DeleteAsync(image);

        Assert.False(File.Exists(expectedPath));
        var dbImage = await _db.PdfDesignImages.FindAsync(id);
        Assert.Null(dbImage);
    }

    public void Dispose()
    {
        _db.Dispose();
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }
}

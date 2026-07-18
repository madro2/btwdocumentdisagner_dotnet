using System;
using System.Threading.Tasks;
using BtwDocumentDesigner.Application.Services;
using BtwDocumentDesigner.Application.Interfaces;
using BtwDocumentDesigner.Domain;
using Moq;
using Xunit;

namespace BtwDocumentDesigner.Tests.Application
{
    public class DesignServiceTests
    {
        private readonly Mock<IDesignRepository> _designRepositoryMock;
        private readonly DesignService _designService;

        public DesignServiceTests()
        {
            // Configuramos el Mock
            _designRepositoryMock = new Mock<IDesignRepository>();
            
            // Inyectamos el Mock en el servicio
            _designService = new DesignService(_designRepositoryMock.Object);
        }

        [Fact]
        public async Task CreateDesignAsync_WhenDesignExists_ThrowsInvalidOperationException()
        {
            // Arrange
            var newDesign = new PdfDesignTemplate 
            { 
                DesignName = "Contrato", 
                DesignVersion = 1 
            };

            // Simulamos que el repositorio responde que SÍ existe
            _designRepositoryMock
                .Setup(repo => repo.ExistsAsync(It.IsAny<string>(), It.IsAny<int>()))
                .ReturnsAsync(true);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _designService.CreateDesignAsync(newDesign));

            Assert.Equal("Ya existe un diseño con ese nombre y versión.", exception.Message);
        }
        
        [Fact]
        public async Task CreateDesignAsync_WhenDesignDoesNotExist_AddsDesignSuccessfully()
        {
            // Arrange
            var newDesign = new PdfDesignTemplate 
            { 
                DesignName = "Factura", 
                DesignVersion = 1 
            };

            // Simulamos que el repositorio responde que NO existe
            _designRepositoryMock
                .Setup(repo => repo.ExistsAsync(It.IsAny<string>(), It.IsAny<int>()))
                .ReturnsAsync(false);

            // Act
            var result = await _designService.CreateDesignAsync(newDesign);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Factura", result.DesignName);
            
            // Verificamos que el repositorio haya llamado a AddAsync al menos 1 vez
            _designRepositoryMock.Verify(repo => repo.AddAsync(It.IsAny<PdfDesignTemplate>()), Times.Once);
        }
    }
}

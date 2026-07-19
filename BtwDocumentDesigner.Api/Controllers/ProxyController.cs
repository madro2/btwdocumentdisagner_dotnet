using Microsoft.AspNetCore.Mvc;

namespace BtwDocumentDesigner.Api.Controllers
{
    [ApiController]
    [Route("DocumentDesignerApi/[controller]")]
    public class ProxyController : ControllerBase
    {
        private readonly HttpClient _httpClient;

        public ProxyController(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _httpClient.BaseAddress = new Uri("https://apiconnect.febtw.co");
        }

        [HttpGet("filesfe/FilesFE/{id}/XMLERP/WithPath")]
        public async Task<IActionResult> GetXml(string id)
        {
            try
            {
                var response = await _httpClient.GetAsync($"/filesfe/FilesFE/{id}/XMLERP/WithPath");
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsByteArrayAsync();
                    var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/xml";
                    return File(content, contentType);
                }
                
                return StatusCode((int)response.StatusCode, await response.Content.ReadAsStringAsync());
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }
    }
}

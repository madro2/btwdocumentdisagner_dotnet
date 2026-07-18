using Microsoft.AspNetCore.Mvc.Formatters;

namespace BtwDocumentDesigner.Api.Formatters
{
    public class RawStringInputFormatter : InputFormatter
    {
        public RawStringInputFormatter()
        {
            SupportedMediaTypes.Add("text/plain");
            SupportedMediaTypes.Add("application/xml");
            SupportedMediaTypes.Add("application/json");
        }

        protected override bool CanReadType(Type type)
        {
            return type == typeof(string);
        }

        public override async Task<InputFormatterResult> ReadRequestBodyAsync(InputFormatterContext context)
        {
            var request = context.HttpContext.Request;
            using var reader = new StreamReader(request.Body);
            var content = await reader.ReadToEndAsync();
            return await InputFormatterResult.SuccessAsync(content);
        }
    }
}

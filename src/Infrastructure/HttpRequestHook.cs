using Newtonsoft.Json;
using Ntrada;
using Ntrada.Extensions.RabbitMq;
using Ntrada.Hooks;
using System.Net.Http;
using System.Threading.Tasks;

namespace Spirebyte.APIGateway.Infrastructure
{
    internal sealed class HttpRequestHook : IHttpRequestHook
    {
        private readonly IContextBuilder _contextBuilder;

        public HttpRequestHook(IContextBuilder contextBuilder)
        {
            _contextBuilder = contextBuilder;
        }


        public Task InvokeAsync(HttpRequestMessage request, ExecutionData data)
        {
            var context = JsonConvert.SerializeObject(_contextBuilder.Build(data));
            request.Headers.TryAddWithoutValidation("Correlation-Context", context);

            return Task.CompletedTask;
        }
    }
}
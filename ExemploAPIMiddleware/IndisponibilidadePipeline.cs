using Microsoft.AspNetCore.Builder;

namespace ExemploAPIMiddleware
{
    public class IndisponibilidadePipeline
    {
        public void Configure(IApplicationBuilder applicationBuilder)
        {
            applicationBuilder.UseChecagemIndisponibilidade();
        }
    }
}
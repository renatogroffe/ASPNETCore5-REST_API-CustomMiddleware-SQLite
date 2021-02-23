using System;
using System.Threading.Tasks;
using System.Data;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using FluentMigrator.Runner;
using System.Data.SQLite;

namespace ExemploAPIMiddleware
{
    public class ChecagemIndisponibilidade
    {
        private readonly RequestDelegate _next;

        public ChecagemIndisponibilidade(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext httpContext)
        {
            var config = (IConfiguration)httpContext
                .RequestServices.GetService(typeof(IConfiguration));
            var logger = (ILogger<ChecagemIndisponibilidade>)httpContext
                .RequestServices.GetService(typeof(ILogger<ChecagemIndisponibilidade>));

            using var conexao = new SQLiteConnection(
                config.GetConnectionString("BaseConfigDisponibilidade"));
            conexao.Open();
            string mensagem = null;

            var cmd = conexao.CreateCommand();
            cmd.CommandText =
                "SELECT Mensagem FROM Indisponibilidade " +
                "WHERE @DataProcessamento BETWEEN InicioIndisponibilidade " +
                    "AND TerminoIndisponibilidade " +
                "ORDER BY InicioIndisponibilidade LIMIT 1";
            cmd.Parameters.Add("@DataProcessamento",
                DbType.DateTime).Value = DateTime.Now;

            logger.LogInformation(
                "Analisando se a aplicacao deve ser considerada como indisponivel...");
            var reader = cmd.ExecuteReader();
            if (reader.Read())
                mensagem = reader["Mensagem"].ToString();

            conexao.Close();

            if (mensagem == null)
            {
                logger.LogInformation("Acesso liberado a aplicacao...");
                await _next(httpContext);
            }
            else
            {
                logger.LogError(
                    $"Aplicacao configurada como indisponivel - Mensagem de retorno: {mensagem}");
                httpContext.Response.StatusCode = 403;
                httpContext.Response.ContentType = "application/json";
                
                var status = new
                {
                    Codigo = 403,
                    Status = "Forbidden",
                    Mensagem = mensagem
                };
                
                await httpContext.Response.WriteAsync(
                    JsonSerializer.Serialize(status));
            }
        }
    }

    public static class ChecagemIndisponibilidadeExtensions
    {
        public static IServiceCollection ConfigureDBChecagemIndisponibilidade(
            this IServiceCollection services,
            string connectionString)
        {
            services.AddFluentMigratorCore()
                .ConfigureRunner(cfg => cfg
                    .AddSQLite()
                    .WithGlobalConnectionString(connectionString)
                    .ScanIn(typeof(Startup).Assembly).For.Migrations()
                )
                .AddLogging(cfg => cfg.AddFluentMigratorConsole());

            var migrationRunner =
                services.BuildServiceProvider(false).GetService<IMigrationRunner>();
            migrationRunner.MigrateUp();

            return services;
        }

        public static IApplicationBuilder UseChecagemIndisponibilidade(
            this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<ChecagemIndisponibilidade>();
        }
    }
}
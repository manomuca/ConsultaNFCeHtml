using System.Net;
using System.Net.Http.Headers;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpClient("nfce", client =>
{
    client.Timeout = TimeSpan.FromSeconds(45);
    client.DefaultRequestHeaders.UserAgent.ParseAdd(
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
        "(KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36");
    client.DefaultRequestHeaders.Accept.Add(
        new MediaTypeWithQualityHeaderValue("text/html"));
});

var app = builder.Build();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapPost("/api/consultar", async (ConsultaRequest request, IHttpClientFactory factory, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request.Url) ||
        !Uri.TryCreate(request.Url, UriKind.Absolute, out var uri) ||
        (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
    {
        return Results.BadRequest(new { erro = "Informe uma URL HTTP ou HTTPS válida." });
    }

    // Este protótipo só permite consultar o domínio da SEFAZ-RJ indicado para o teste.
    if (!string.Equals(uri.Host, "consultadfe.fazenda.rj.gov.br", StringComparison.OrdinalIgnoreCase))
    {
        return Results.BadRequest(new { erro = "Por segurança, este protótipo aceita apenas consultadfe.fazenda.rj.gov.br." });
    }

    try
    {
        var client = factory.CreateClient("nfce");
        using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseContentRead, ct);
        var html = await response.Content.ReadAsStringAsync(ct);

        return Results.Ok(new
        {
            statusHttp = (int)response.StatusCode,
            descricaoStatus = response.StatusCode.ToString(),
            urlFinal = response.RequestMessage?.RequestUri?.ToString() ?? request.Url,
            tipoConteudo = response.Content.Headers.ContentType?.ToString() ?? "(não informado)",
            tamanhoCaracteres = html.Length,
            html
        });
    }
    catch (TaskCanceledException)
    {
        return Results.Problem("A consulta excedeu o tempo limite de 45 segundos.", statusCode: 504);
    }
    catch (HttpRequestException ex)
    {
        return Results.Problem("Não foi possível consultar a SEFAZ: " + ex.Message, statusCode: 502);
    }
});

app.Run();

record ConsultaRequest(string Url);


using System.Net.Http.Headers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient("nfce", client =>
{
    client.Timeout = TimeSpan.FromSeconds(45);

    client.DefaultRequestHeaders.UserAgent.ParseAdd(
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
        "AppleWebKit/537.36 (KHTML, like Gecko) " +
        "Chrome/130.0.0.0 Safari/537.36");

    client.DefaultRequestHeaders.Accept.Add(
        new MediaTypeWithQualityHeaderValue("text/html"));
});

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapPost("/api/consultar", async (
    ConsultaRequest request,
    IHttpClientFactory factory,
    CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request.Url) ||
        !Uri.TryCreate(request.Url, UriKind.Absolute, out var uri) ||
        uri.Scheme != Uri.UriSchemeHttps)
    {
        return Results.BadRequest(new
        {
            erro = "Informe uma URL HTTPS válida."
        });
    }



    try
    {
        var client = factory.CreateClient("nfce");

        using var response = await client.GetAsync(
            uri,
            HttpCompletionOption.ResponseContentRead,
            ct);

        var html = await response.Content.ReadAsStringAsync(ct);

        bool bloqueioSefaz =
            html.Contains("endereços IP", StringComparison.OrdinalIgnoreCase) ||
            html.Contains("endereços IP usados", StringComparison.OrdinalIgnoreCase) ||
            html.Contains("bloqueia acessos", StringComparison.OrdinalIgnoreCase) ||
            html.Contains("OuvERJ", StringComparison.OrdinalIgnoreCase);

        return Results.Ok(new
        {
            statusHttp = (int)response.StatusCode,
            descricaoStatus = response.StatusCode.ToString(),
            urlFinal = response.RequestMessage?.RequestUri?.ToString()
                       ?? request.Url,
            tipoConteudo = response.Content.Headers.ContentType?.ToString()
                           ?? "(não informado)",
            tamanhoCaracteres = html.Length,
            bloqueioSefaz,
            mensagem = bloqueioSefaz
                ? "A SEFAZ retornou uma página de bloqueio. A nota fiscal não foi obtida."
                : response.IsSuccessStatusCode
                    ? "Resposta recebida. Confira se o HTML contém os dados da nota."
                    : "A SEFAZ retornou um erro HTTP.",
            html
        });
    }
    catch (TaskCanceledException)
    {
        return Results.Problem(
            "A consulta excedeu o tempo limite de 45 segundos.",
            statusCode: 504);
    }
    catch (HttpRequestException ex)
    {
        return Results.Problem(
            "Não foi possível consultar a SEFAZ. Detalhes: " + ex.Message,
            statusCode: 502);
    }
});

app.Run();

record ConsultaRequest(string Url);

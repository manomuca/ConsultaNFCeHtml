using PuppeteerSharp;

var builder = WebApplication.CreateBuilder(args);

// Baixa automaticamente o Chromium compatível na inicialização do app (se não existir)
Console.WriteLine("Verificando/Baixando estrutura do Chromium...");
var browserFetcher = new BrowserFetcher();
await browserFetcher.DownloadAsync();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapPost("/api/consultar", async (
    [Microsoft.AspNetCore.Mvc.FromBody] ConsultaRequest request,
    CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request.Url) ||
        !Uri.TryCreate(request.Url, UriKind.Absolute, out var uri) ||
        uri.Scheme != Uri.UriSchemeHttps)
    {
        return Results.BadRequest(new { erro = "Informe uma URL HTTPS válida." });
    }

    var launchOptions = new LaunchOptions
    {
        Headless = true,
        Args = [
            "--no-sandbox",
            "--disable-setuid-sandbox",
            "--disable-blink-features=AutomationControlled"
        ]
    };

    IBrowser? browser = null;
    try
    {
        browser = await Puppeteer.LaunchAsync(launchOptions);
        using var page = await browser.NewPageAsync();

        await page.SetUserAgentAsync("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36");

        var navigationOptions = new NavigationOptions { WaitUntil = [WaitUntilNavigation.Networkidle2], Timeout = 30000 };
        await page.GoToAsync(request.Url, navigationOptions);

        var seletorProdutos = "#tabResult, .table, #conteudo";
        await page.WaitForSelectorAsync(seletorProdutos, new WaitForSelectorOptions { Timeout = 15000 });

        var html = await page.GetContentAsync();

        bool bloqueioSefaz =
            html.Contains("endereços IP", StringComparison.OrdinalIgnoreCase) ||
            html.Contains("endereços IP usados", StringComparison.OrdinalIgnoreCase) ||
            html.Contains("bloqueia acessos", StringComparison.OrdinalIgnoreCase) ||
            html.Contains("OuvERJ", StringComparison.OrdinalIgnoreCase);

        return Results.Ok(new
        {
            statusHttp = 200,
            descricaoStatus = "OK",
            urlFinal = page.Url,
            tipoConteudo = "text/html; charset=utf-8",
            tamanhoCaracteres = html.Length,
            bloqueioSefaz,
            mensagem = bloqueioSefaz
                ? "A SEFAZ retornou uma página de bloqueio. A nota fiscal não foi obtida."
                : "Resposta recebida com sucesso. O HTML completo foi capturado pelo Puppeteer.",
            html
        });
    }
    catch (WaitTaskTimeoutException)
    {
        return Results.Problem(
            "O tempo limite esgotou esperando a SEFAZ renderizar os produtos (Possível barreira de Captcha).",
            statusCode: 504);
    }
    catch (Exception ex)
    {
        return Results.Problem(
            "Não foi possível processar a consulta via automação. Detalhes: " + ex.Message,
            statusCode: 502);
    }
    finally
    {
        if (browser != null)
        {
            await browser.CloseAsync();
        }
    }
});

app.Run();

public record ConsultaRequest(string Url);

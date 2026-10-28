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
    // Validação da URL recebida do front-end
    if (string.IsNullOrWhiteSpace(request.Url) ||
        !Uri.TryCreate(request.Url, UriKind.Absolute, out var uri) ||
        uri.Scheme != Uri.UriSchemeHttps)
    {
        return Results.BadRequest(new { erro = "Informe uma URL HTTPS válida." });
    }

    // Configurações para rodar o navegador em modo oculto (Headless)
    var launchOptions = new LaunchOptions
    {
        Headless = true,
        Args = [
            "--no-sandbox",
            "--disable-setuid-sandbox",
            "--disable-blink-features=AutomationControlled" // Oculta propriedades de automação
        ]
    };

    IBrowser? browser = null;
    try
    {
        // 1. Inicializa o motor do navegador Chromium
        browser = await Puppeteer.LaunchAsync(launchOptions);
        using var page = await browser.NewPageAsync();

        // 2. Define um User-Agent moderno para evitar bloqueios simples
        await page.SetUserAgentAsync("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36");

        // 3. Navega até o link do QR Code da NFC-e
        var navigationOptions = new NavigationOptions { WaitUntil = [WaitUntilNavigation.Networkidle2], Timeout = 30000 };
        await page.GoToAsync(request.Url, navigationOptions);

        // 4. Aguarda até que a tabela ou o bloco com os dados reais apareçam na tela
        var seletorProdutos = "#tabResult, .table, #conteudo";
        await page.WaitForSelectorAsync(seletorProdutos, new WaitForSelectorOptions { Timeout = 15000 });

        // 5. Captura o HTML final completamente renderizado pela SEFAZ
        var html = await page.GetContentAsync();

        // Valida se o HTML retornado contém mensagens conhecidas de bloqueio de IP
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
        // Certifica o fechamento definitivo do processo do Chromium para não vazar memória RAM
        if (browser != null)
        {
            await browser.CloseAsync();
        }
    }
});

app.Run();

// Record que mapeia a requisição JSON vinda do fetch do JavaScript
public record ConsultaRequest(string Url);

using PuppeteerSharp;
// 1. [INCLUSÃO] Importa os plugins instalados via terminal para mascarar o robô
using PuppeteerExtraSharp;
using PuppeteerExtraSharp.Plugins.ExtraStealth;

var builder = WebApplication.CreateBuilder(args);

// Baixa automaticamente o Chromium compatível na inicialização do app (se não existir)
Console.WriteLine("Verificando/Baixando estrutura do Chromium...");
var browserFetcher = new BrowserFetcher();
await browserFetcher.DownloadAsync();

var app = builder.Build();

// 2. [CONFIGURAÇÃO] Ativa o uso de arquivos estáticos da pasta wwwroot (index.html)
app.UseDefaultFiles();
app.UseStaticFiles();

// 3. [ROTA] Mapeamento do ponto de consulta que o seu fetch() do JavaScript chama
app.MapPost("/api/consultar", async (
    [Microsoft.AspNetCore.Mvc.FromBody] ConsultaRequest request,
    CancellationToken ct) =>
{
    // Valida se o link enviado é um link seguro válido
    if (string.IsNullOrWhiteSpace(request.Url) ||
        !Uri.TryCreate(request.Url, UriKind.Absolute, out var uri) ||
        uri.Scheme != Uri.UriSchemeHttps)
    {
        return Results.BadRequest(new { erro = "Informe uma URL HTTPS válida." });
    }

    var launchOptions = new LaunchOptions
    {
        Headless = true, // Mantém oculto no servidor
        Args = [
            "--no-sandbox",
            "--disable-setuid-sandbox",
            "--disable-blink-features=AutomationControlled"
        ]
    };

    IBrowser? browser = null;
    try
    {
        // 4. [CAMUFLAGEM] Instancia o PuppeteerExtra injetando o plugin Stealth contra o Captcha
        var extra = new PuppeteerExtra();
        extra.Use(new StealthPlugin()); 
        
        // Abre o navegador modificado
        browser = await extra.LaunchAsync(launchOptions);
        using var page = await browser.NewPageAsync();

        // Força uma resolução de tela padrão de computador (evita suspeitas da SEFAZ)
        await page.SetViewportAsync(new ViewPortOptions { Width = 1366, Height = 768 });

        // Aplica o User-Agent que simula um navegador real atualizado
        await page.SetUserAgentAsync("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36");

        // Acessa a nota fiscal da SEFAZ-RJ
        var navigationOptions = new NavigationOptions { WaitUntil = [WaitUntilNavigation.Networkidle2], Timeout = 30000 };
        await page.GoToAsync(request.Url, navigationOptions);

        // 5. [FLEXIBILIZAÇÃO] Aguarda que a estrutura da página seja montada
        var seletorProdutos = "#conteudo, #principal, body";
        await page.WaitForSelectorAsync(seletorProdutos, new WaitForSelectorOptions { Timeout = 20000 });

        // Captura o HTML com os dados na variável
        var html = await page.GetContentAsync();

        // Verifica assinaturas de bloqueio comuns por IP
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
                : "Resposta recebida com sucesso. O HTML completo foi capturado pelo Puppeteer com camuflagem.",
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
        // Fecha o navegador de forma segura para poupar recursos
        if (browser != null)
        {
            await browser.CloseAsync();
        }
    }
});

app.Run();

// Estrutura que recebe os dados do seu Front-end
public record ConsultaRequest(string Url);

PROJETO: ConsultaNFCeHtml

Requisitos:
- .NET 8 SDK instalado no Windows.
- Acesso à internet.

Como executar:
1. Extraia o ZIP para uma pasta.
2. Abra o Prompt de Comando nessa pasta.
3. Execute: dotnet run
4. Abra no navegador o endereço local mostrado no terminal (normalmente http://localhost:xxxx).
5. A URL da NFC-e já estará preenchida. Clique em "Consultar e obter HTML".
6. Para guardar o retorno, clique em "Salvar HTML em arquivo".

O que este projeto faz:
- Faz uma requisição HTTP à URL informada.
- Guarda o corpo da resposta em uma variável string chamada html no servidor.
- Exibe o retorno na tela e permite salvá-lo como arquivo HTML.
- Não usa banco de dados nem altera o projeto original.

Limitação importante:
A SEFAZ pode construir a página final por JavaScript, frames ou chamadas adicionais. Nesse caso, uma requisição HTTP direta pode não retornar a lista de produtos. O resultado HTTP e o HTML permitem diagnosticar isso; se necessário, o próximo passo será um navegador automatizado, como Playwright.

Segurança:
O protótipo aceita apenas URLs do host consultadfe.fazenda.rj.gov.br para evitar que a tela seja usada para consultar endereços arbitrários.

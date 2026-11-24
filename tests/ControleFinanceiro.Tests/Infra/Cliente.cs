using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ControleFinanceiro.Tests.Infra;

// Resposta da API: o código (200, 400...) e o JSON.
public record Resposta(int Status, JsonNode? Dados)
{
    // Quando a API responde só um texto (as mensagens de erro), ele fica aqui.
    public string? Mensagem => Dados?.GetValueKind() == JsonValueKind.String ? Dados.GetValue<string>() : null;

    public decimal Decimal(string campo) => Dados![campo]!.GetValue<decimal>();
}

// Faz requisições como o navegador: guarda o cookie de login entre uma e outra.
public class Cliente(HttpClient http)
{
    public string Email { get; private set; } = "";

    // Cria um usuário novo (e-mail único) e já entra com ele.
    public static async Task<Cliente> NovoUsuario(ApiDeTeste api, string senha = "senha123")
    {
        var cliente = new Cliente(api.CreateClient());
        cliente.Email = $"{Guid.NewGuid():N}@teste.com";
        var r = await cliente.Post("/auth/cadastro", new { email = cliente.Email, senha });
        Assert.Equal(201, r.Status);
        return cliente;
    }

    public static Cliente SemLogin(ApiDeTeste api) => new(api.CreateClient());

    public Task<Resposta> Get(string caminho) => Enviar(HttpMethod.Get, caminho, null);
    public Task<Resposta> Post(string caminho, object? corpo = null) => Enviar(HttpMethod.Post, caminho, corpo);
    public Task<Resposta> Put(string caminho, object? corpo) => Enviar(HttpMethod.Put, caminho, corpo);
    public Task<Resposta> Delete(string caminho) => Enviar(HttpMethod.Delete, caminho, null);

    private async Task<Resposta> Enviar(HttpMethod metodo, string caminho, object? corpo)
    {
        var pedido = new HttpRequestMessage(metodo, caminho);
        if (corpo is not null)
            pedido.Content = JsonContent.Create(corpo);
        var resposta = await http.SendAsync(pedido);
        var texto = await resposta.Content.ReadAsStringAsync();
        return new Resposta((int)resposta.StatusCode, texto.Length > 0 ? JsonNode.Parse(texto) : null);
    }

    // Atalhos para montar os dados dos testes.
    public async Task<int> CriarCategoria(string nome = "Geral", decimal? metaMensal = null)
    {
        var r = await Post("/categorias", new { nome, metaMensal });
        Assert.Equal(201, r.Status);
        return r.Dados!["id"]!.GetValue<int>();
    }

    public async Task<JsonNode> CriarTransacao(int categoriaId, string descricao, decimal valor, string data,
        string tipo = "Despesa", bool pago = true, string consulta = "")
    {
        var r = await Post($"/transacoes{consulta}", new { descricao, valor, data, tipo, categoriaId, pago });
        Assert.Equal(201, r.Status);
        return r.Dados!;
    }

    // Todas as transações cuja descrição começa com o texto, da mais antiga para a mais nova.
    public async Task<List<JsonNode>> Transacoes(string comecaCom = "")
    {
        var r = await Get("/transacoes");
        return r.Dados!.AsArray()
            .Select(t => t!)
            .Where(t => t["descricao"]!.GetValue<string>().StartsWith(comecaCom))
            .OrderBy(t => t["data"]!.GetValue<string>())
            .ToList();
    }
}

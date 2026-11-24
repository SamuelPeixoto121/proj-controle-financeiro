using ControleFinanceiro.Tests.Infra;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ControleFinanceiro.Tests;

[Collection("api")]
public class AutenticacaoTestes(ApiDeTeste api)
{
    [Theory]
    [InlineData("/categorias")]
    [InlineData("/transacoes")]
    [InlineData("/saldo")]
    [InlineData("/despesas-por-categoria")]
    [InlineData("/resumo-mensal")]
    [InlineData("/pendentes")]
    [InlineData("/metas?mes=2026-11")]
    [InlineData("/auth/eu")]
    public async Task Sem_login_as_rotas_respondem_401(string rota)
    {
        var r = await Cliente.SemLogin(api).Get(rota);
        Assert.Equal(401, r.Status);
    }

    [Theory]
    [InlineData("sem-arroba", "senha123", "Informe um e-mail válido.")]
    [InlineData("a@b.com", "123", "A senha deve ter pelo menos 6 caracteres.")]
    public async Task Cadastro_invalido_da_400(string email, string senha, string mensagem)
    {
        var r = await Cliente.SemLogin(api).Post("/auth/cadastro", new { email, senha });
        Assert.Equal(400, r.Status);
        Assert.Equal(mensagem, r.Mensagem);
    }

    [Fact]
    public async Task Email_e_guardado_em_minusculas_e_repetido_da_409()
    {
        var email = $"{Guid.NewGuid():N}@Teste.COM";
        var r = await Cliente.SemLogin(api).Post("/auth/cadastro", new { email = $"  {email} ", senha = "senha123" });
        Assert.Equal(201, r.Status);
        Assert.Equal(email.ToLowerInvariant(), r.Dados!["email"]!.GetValue<string>());

        var repetido = await Cliente.SemLogin(api).Post("/auth/cadastro", new { email = email.ToUpperInvariant(), senha = "outra123" });
        Assert.Equal(409, repetido.Status);
    }

    [Fact]
    public async Task Cookie_de_login_e_HttpOnly_e_SameSite_Lax()
    {
        var http = api.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var resposta = await http.PostAsync("/auth/cadastro",
            System.Net.Http.Json.JsonContent.Create(new { email = $"{Guid.NewGuid():N}@teste.com", senha = "senha123" }));
        var cookie = string.Join(";", resposta.Headers.GetValues("Set-Cookie")).ToLowerInvariant();
        Assert.Contains("controle_financeiro_sessao=", cookie);
        Assert.Contains("httponly", cookie);
        Assert.Contains("samesite=lax", cookie);
    }

    [Fact]
    public async Task Login_certo_entra_e_errado_da_401()
    {
        var usuario = await Cliente.NovoUsuario(api, "minhaSenha1");

        var errado = await Cliente.SemLogin(api).Post("/auth/login", new { email = usuario.Email, senha = "chute" });
        Assert.Equal(401, errado.Status);
        Assert.Equal("E-mail ou senha incorretos.", errado.Mensagem);

        var outro = Cliente.SemLogin(api);
        Assert.Equal(200, (await outro.Post("/auth/login", new { email = usuario.Email, senha = "minhaSenha1" })).Status);
        Assert.Equal(usuario.Email, (await outro.Get("/auth/eu")).Dados!["email"]!.GetValue<string>());
    }

    [Fact]
    public async Task Logout_encerra_a_sessao()
    {
        var usuario = await Cliente.NovoUsuario(api);
        Assert.Equal(200, (await usuario.Get("/auth/eu")).Status);
        Assert.Equal(204, (await usuario.Post("/auth/logout")).Status);
        Assert.Equal(401, (await usuario.Get("/auth/eu")).Status);
    }

    [Fact]
    public async Task Trocar_senha_valida_e_troca()
    {
        var usuario = await Cliente.NovoUsuario(api, "antiga123");

        Assert.Equal("Senha atual incorreta.", (await usuario.Post("/auth/trocar-senha", new { senhaAtual = "errada", novaSenha = "nova12345" })).Mensagem);
        Assert.Equal(400, (await usuario.Post("/auth/trocar-senha", new { senhaAtual = "antiga123", novaSenha = "123" })).Status);
        Assert.Equal(400, (await usuario.Post("/auth/trocar-senha", new { senhaAtual = "antiga123", novaSenha = "antiga123" })).Status);

        Assert.Equal(204, (await usuario.Post("/auth/trocar-senha", new { senhaAtual = "antiga123", novaSenha = "nova12345" })).Status);
        Assert.Equal(200, (await usuario.Get("/auth/eu")).Status); // continua logado
        Assert.Equal(401, (await Cliente.SemLogin(api).Post("/auth/login", new { email = usuario.Email, senha = "antiga123" })).Status);
        Assert.Equal(200, (await Cliente.SemLogin(api).Post("/auth/login", new { email = usuario.Email, senha = "nova12345" })).Status);
    }

    [Fact]
    public async Task Sexta_tentativa_de_login_no_minuto_da_429()
    {
        // Uma API separada com o limite de verdade (5 por minuto).
        await using var apiComLimite = new ApiComLimiteReal();
        var cliente = Cliente.SemLogin(apiComLimite);
        for (var i = 1; i <= 5; i++)
            Assert.Equal(401, (await cliente.Post("/auth/login", new { email = "ninguem@teste.com", senha = "chute" })).Status);

        var sexta = await cliente.Post("/auth/login", new { email = "ninguem@teste.com", senha = "chute" });
        Assert.Equal(429, sexta.Status);
        Assert.Equal("Muitas tentativas. Aguarde um minuto e tente de novo.", sexta.Mensagem);
    }
}

using ControleFinanceiro.Tests.Infra;

// Um teste de cada vez: todos usam o mesmo banco de testes.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace ControleFinanceiro.Tests.Infra;

// Todas as classes marcadas com [Collection("api")] dividem a mesma ApiDeTeste
// (o banco é criado uma vez no começo e apagado no fim).
[CollectionDefinition("api")]
public class ColecaoApi : ICollectionFixture<ApiDeTeste>;

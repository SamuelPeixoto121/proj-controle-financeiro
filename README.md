# Controle Financeiro

Controle financeiro pessoal: cadastro de receitas e despesas, organização por categorias e saldo total.

## Funcionalidades

- **Login**: cada pessoa cria a própria conta (e-mail e senha) e só vê os próprios dados
- Cadastrar, editar e excluir **transações** (receitas e despesas), com descrição, valor, data e categoria
- Cadastrar, renomear e excluir **categorias** (não é possível excluir uma categoria que tenha transações)
- **Saldo total**: soma das receitas, soma das despesas e saldo (receitas − despesas), atualizado a cada alteração
- **Filtro por mês**: ver as transações e o saldo de um mês específico, ou de todos os meses
- **Busca e filtros na lista de transações**: por descrição (sem diferenciar maiúsculas e acentos), categoria e tipo, com a soma do que está filtrado
- **Exportar para CSV**: baixa as transações que estão na tabela (respeitando o mês e os filtros), num formato que o Excel em português abre direto
- **Gráfico de despesas por categoria**: barras da maior para a menor despesa, com valor e porcentagem
- **Resumo mês a mês**: receitas e despesas de cada mês do ano lado a lado, em gráfico e em tabela com o saldo de cada mês

## Tecnologias

| Parte | Tecnologias |
|---|---|
| Back-end | ASP.NET Core (.NET 11) com Minimal APIs, Swagger |
| Banco de dados | MySQL 8, Entity Framework Core 9 com Pomelo.EntityFrameworkCore.MySql |
| Front-end | React 19 com Vite, em JavaScript |

> O EF Core está na versão 9 porque o Pomelo (provedor do MySQL) ainda não tem versão para o EF Core 10/11. O EF Core 9 funciona normalmente no .NET 11.

## Pré-requisitos

- [.NET SDK 11](https://dotnet.microsoft.com/download)
- [Node.js](https://nodejs.org/) 20.19 ou superior (ou 22.12+)
- MySQL Server 8 (o MySQL Workbench é opcional, só para visualizar os dados)
- Ferramenta de migrations do Entity Framework, na mesma versão do EF Core do projeto:

```powershell
dotnet tool install --global dotnet-ef --version 9.0.23
```

## Como rodar

Os comandos abaixo são para o PowerShell, no Windows.

### 1. Clonar o repositório

```powershell
git clone https://github.com/SamuelPeixoto121/proj-controle-financeiro.git
cd proj-controle-financeiro
```

### 2. Configurar a conexão com o banco

A senha do MySQL **não fica no repositório**. Ela é guardada com os User Secrets do .NET, fora da pasta do projeto. Troque `SUA_SENHA` (e o `User`, se não usar o `root`):

```powershell
cd backend
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;Port=3306;Database=controle_financeiro;User=root;Password=SUA_SENHA"
```

### 3. Criar o banco e as tabelas

Ainda na pasta `backend`:

```powershell
dotnet ef database update
```

Esse comando cria o banco `controle_financeiro` (se ainda não existir) e as tabelas `Categorias` e `Transacoes`.

### 4. Rodar a API

Ainda na pasta `backend`:

```powershell
dotnet run
```

A API sobe em **http://localhost:5083**. A documentação interativa (Swagger) fica em **http://localhost:5083/swagger**.

### 5. Rodar o front-end

Em **outro terminal**, a partir da raiz do projeto:

```powershell
cd frontend
npm install
npm run dev
```

Abra **http://localhost:5173** no navegador e clique em **Criar conta**. A API precisa estar rodando ao mesmo tempo.

> Se o banco já tinha categorias e transações de antes do login existir, elas passam a ser da **primeira conta criada**.

## Rotas da API

O login usa um cookie de sessão (`HttpOnly`), que o navegador envia sozinho em cada requisição.

| Método | Rota | Descrição |
|---|---|---|
| POST | `/auth/cadastro` | Cria uma conta e já entra (`{ "email": "...", "senha": "..." }`, senha com 6+ caracteres) |
| POST | `/auth/login` | Entra com e-mail e senha |
| POST | `/auth/logout` | Sai |
| GET | `/auth/eu` | Mostra quem está logado (401 se ninguém) |

As rotas abaixo **exigem login** (sem login, respondem 401) e só enxergam os dados do usuário logado.

| Método | Rota | Descrição |
|---|---|---|
| GET | `/categorias` | Lista as categorias |
| GET | `/categorias/{id}` | Busca uma categoria |
| POST | `/categorias` | Cria uma categoria |
| PUT | `/categorias/{id}` | Renomeia uma categoria |
| DELETE | `/categorias/{id}` | Exclui uma categoria (409 se tiver transações) |
| GET | `/transacoes` | Lista as transações, da mais recente para a mais antiga |
| GET | `/transacoes/{id}` | Busca uma transação |
| POST | `/transacoes` | Cria uma transação |
| PUT | `/transacoes/{id}` | Edita uma transação |
| DELETE | `/transacoes/{id}` | Exclui uma transação |
| GET | `/saldo` | Total de receitas, total de despesas e saldo |
| GET | `/despesas-por-categoria` | Total de despesas de cada categoria, do maior para o menor |
| GET | `/resumo-mensal?ano=AAAA` | Receitas, despesas e saldo de cada um dos 12 meses do ano (sem `ano`, usa o ano atual) |

`GET /transacoes`, `GET /saldo` e `GET /despesas-por-categoria` aceitam o filtro opcional `?mes=AAAA-MM` (por exemplo, `/saldo?mes=2026-11`) para considerar só um mês. Sem o filtro, consideram todas as transações.

Exemplo de corpo para criar uma transação:

```json
{
  "descricao": "Salário",
  "valor": 3500.50,
  "data": "2026-11-05",
  "tipo": "Receita",
  "categoriaId": 1
}
```

O campo `tipo` aceita `"Receita"` ou `"Despesa"`. O `valor` é sempre positivo: é o tipo que define se o dinheiro entra ou sai.

## Estrutura de pastas

```
proj-controle-financeiro/
├── backend/                 API em ASP.NET Core
│   ├── Data/                AppDbContext (configuração do banco)
│   ├── Migrations/          Migrations do Entity Framework
│   ├── Models/              Usuario, Categoria, Transacao e TipoTransacao
│   └── Program.cs           Configuração da API, login e todas as rotas
└── frontend/                Front-end em React
    └── src/
        ├── api.js           Função que chama a API e trata os erros
        ├── formatar.js      Formatação de valores (R$) e datas
        ├── csv.js           Geração e download do arquivo CSV
        ├── App.jsx          Página principal, filtro por mês e saldo
        ├── Login.jsx        Tela de entrar e criar conta
        ├── ResumoMensal.jsx Resumo mês a mês (gráfico e tabela)
        ├── DespesasPorCategoria.jsx  Gráfico de despesas por categoria
        ├── Transacoes.jsx   Tela de transações
        └── Categorias.jsx   Tela de categorias
```

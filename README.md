# Controle Financeiro

[![Testes](https://github.com/SamuelPeixoto121/proj-controle-financeiro/actions/workflows/testes.yml/badge.svg)](https://github.com/SamuelPeixoto121/proj-controle-financeiro/actions/workflows/testes.yml)

Controle financeiro pessoal: cadastro de receitas e despesas, organização por categorias e saldo total.

## Funcionalidades

- **Login**: cada pessoa cria a própria conta (e-mail e senha) e só vê os próprios dados, e pode trocar a senha depois
- Cadastrar, editar e excluir **transações** (receitas e despesas), com descrição, valor, data e categoria
- **Contas a pagar (pago / pendente)**: cada transação fica paga/recebida ou pendente (um clique alterna). A visão geral lista as contas pendentes do mês e as atrasadas, com os totais a pagar e a receber
- **Compras parceladas**: ao criar, dá para parcelar o valor total em até 60x ("Geladeira (1/10)" ... "(10/10)", uma por mês). A 1ª parcela fica com os centavos que sobram, para a soma dar exatamente o total
- **Transações recorrentes**: ao criar, dá para repetir a mesma transação por até 60 meses (por exemplo, o aluguel do ano todo de uma vez). As repetições formam uma **série** (marcada com ↻): ao editar ou excluir uma delas, dá para aplicar a mudança também aos meses seguintes
- Cadastrar, editar e excluir **categorias** (não é possível excluir uma categoria que tenha transações)
- **Metas de gasto por categoria**: um limite mensal opcional para cada categoria, com barra de progresso e aviso de "perto do limite" (80%) e "estourou" (acima de 100%)
- **Saldo total**: soma das receitas, soma das despesas e saldo (receitas − despesas), atualizado a cada alteração
- **Filtro por mês**: ver as transações e o saldo de um mês específico, ou de todos os meses
- **Saldo real**: o dinheiro que se tem agora, contando só o que já foi pago e recebido (o saldo e o acumulado são previsões, que contam também o que está pendente)
- **Saldo acumulado**: quanto se tem no fim de cada mês, somando o que sobrou (ou faltou) dos meses anteriores (no resumo do topo e no resumo mês a mês)
- **Busca e filtros na lista de transações**: por descrição (sem diferenciar maiúsculas e acentos), categoria e tipo, com a soma do que está filtrado
- **Importar extrato do banco (CSV)**: o arquivo é lido no navegador (separador `;`, `,` ou tab; números como `1.234,56` ou `-50.00`; datas `05/11/2026` ou `2026-11-05`; UTF-8 ou Windows-1252; coluna de valor ou colunas de crédito e débito; opção para fatura de cartão). Depois é só conferir as colunas, escolher as categorias e importar. Transações que já existem vêm desmarcadas, para não duplicar
- **Exportar para CSV**: baixa as transações que estão na tabela (respeitando o mês e os filtros), num formato que o Excel em português abre direto
- **Gráfico de despesas por categoria**: barras da maior para a menor despesa, com valor e porcentagem
- **Resumo mês a mês**: receitas e despesas de cada mês do ano lado a lado, em gráfico e em tabela com o saldo de cada mês, e um gráfico de linha com a evolução do saldo acumulado (linha tracejada nos meses que ainda não chegaram)
- **Comparação com o ano anterior**: receitas, despesas e saldo do ano com a variação em relação ao ano anterior, e as despesas por categoria dos dois anos (as que mais mudaram primeiro). No ano atual, compara o mesmo período (jan até o mês atual) nos dois anos

A tela é dividida em quatro abas: **Visão geral** (números do mês, contas, metas, gráficos e resumo mês a mês), **Transações**, **Categorias** e **Importar**. O layout se adapta ao celular e tem **modo escuro**: por padrão segue o tema do sistema, e o botão 🌙/☀️ no topo troca na hora (a escolha fica guardada no navegador).

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

## Backup automático

Os scripts da pasta `scripts` fazem uma cópia completa do banco (com o `mysqldump`, que vem com o MySQL), compactada em `.zip`, e guardam os **30 backups mais recentes** na pasta **`OneDrive\Backups\ControleFinanceiro`** (assim eles também ficam na nuvem; sem OneDrive, vão para a pasta `backups` do projeto). A senha do banco é lida dos User Secrets e não aparece em lugar nenhum. Os backups têm dados pessoais e **nunca vão para o GitHub** (a pasta `backups/` está no `.gitignore`).

Comandos (no PowerShell, na pasta do projeto):

```powershell
# Ligar o backup automático: todo dia às 21h (ou assim que o PC for ligado, se estava desligado nesse horário)
powershell -ExecutionPolicy Bypass -File scripts\agendar-backup.ps1

# Outro horário
powershell -ExecutionPolicy Bypass -File scripts\agendar-backup.ps1 -Hora 08:30

# Desligar o backup automático
powershell -ExecutionPolicy Bypass -File scripts\agendar-backup.ps1 -Remover

# Fazer um backup agora
powershell -ExecutionPolicy Bypass -File scripts\backup.ps1

# Restaurar um backup (mostra a lista e pede para digitar RESTAURAR)
powershell -ExecutionPolicy Bypass -File scripts\restaurar.ps1
```

- A tarefa aparece no **Agendador de Tarefas** do Windows como "Backup Controle Financeiro" e roda com o seu usuário (não precisa ser administrador).
- Cada execução é anotada em `backup.log`, na mesma pasta dos backups (inclusive os erros).
- **Restaurar substitui os dados atuais** pelos do backup escolhido. Antes, o script faz um backup do estado atual, para dar para desfazer. De preferência, pare a API (Ctrl+C) antes de restaurar.

## Como rodar os testes

O projeto tem testes automatizados para a API e para o front-end. Rode-os antes e depois de mudar o código: se algo quebrar, o teste mostra o que era esperado e o que veio.

O GitHub também roda todos eles sozinho a cada `git push` (arquivo `.github/workflows/testes.yml`, com um MySQL descartável só para os testes): o resultado aparece como ✔/✖ ao lado de cada commit, na aba **Actions** e no selo no topo deste README.

### Testes da API (xUnit)

Precisam do **MySQL ligado** e da conexão configurada nos User Secrets (passo 2 acima). A API não precisa estar rodando.

```powershell
cd tests/ControleFinanceiro.Tests
dotnet test
```

- Os testes ligam a API em memória e usam **um banco só deles, `controle_financeiro_testes`**, criado do zero no começo e apagado no fim. O banco `controle_financeiro` nunca é usado: se a conexão apontar para outro banco, os testes se recusam a rodar.
- Cobrem login e senha (inclusive o limite de 5 tentativas), categorias e metas, transações (repetir, parcelas e centavos, séries, pago/pendente, importação) e os relatórios (saldo, saldo real, acumulado, resumo mensal, metas e contas pendentes), além do isolamento entre usuários.
- Se a API estiver rodando com `dotnet run`, pare-a antes (Ctrl+C): o `dotnet test` compila a API, e o Windows não deixa sobrescrever o programa em uso.

### Testes do front-end (Vitest)

```powershell
cd frontend
npm test
```

Testam as funções que fazem contas e leem dados: leitura de extratos CSV (números, datas, separadores, formatos de banco), prévia das parcelas, situação paga/pendente/atrasada, busca sem acentos, exportação para CSV e as posições do gráfico do acumulado. Os arquivos de teste ficam ao lado do código, com o nome terminando em `.test.js`.

## Rotas da API

O login usa um cookie de sessão (`HttpOnly`), que o navegador envia sozinho em cada requisição.

Para dificultar a descoberta de senhas por tentativa e erro, `/auth/cadastro`, `/auth/login` e `/auth/trocar-senha` aceitam juntas **até 5 tentativas por minuto de cada IP**. A partir da 6ª, respondem **429** até o minuto virar. O contador fica na memória e zera quando a API reinicia.

| Método | Rota | Descrição |
|---|---|---|
| POST | `/auth/cadastro` | Cria uma conta e já entra (`{ "email": "...", "senha": "..." }`, senha com 6+ caracteres) |
| POST | `/auth/login` | Entra com e-mail e senha |
| POST | `/auth/logout` | Sai |
| GET | `/auth/eu` | Mostra quem está logado (401 se ninguém) |
| POST | `/auth/trocar-senha` | Troca a senha de quem está logado (`{ "senhaAtual": "...", "novaSenha": "..." }`) |

As rotas abaixo **exigem login** (sem login, respondem 401) e só enxergam os dados do usuário logado.

| Método | Rota | Descrição |
|---|---|---|
| GET | `/categorias` | Lista as categorias |
| GET | `/categorias/{id}` | Busca uma categoria |
| POST | `/categorias` | Cria uma categoria (`{ "nome": "Mercado", "metaMensal": 800 }`; a meta é opcional) |
| PUT | `/categorias/{id}` | Edita o nome e a meta de uma categoria (`metaMensal: null` remove a meta) |
| DELETE | `/categorias/{id}` | Exclui uma categoria (409 se tiver transações) |
| GET | `/transacoes` | Lista as transações, da mais recente para a mais antiga |
| GET | `/transacoes/{id}` | Busca uma transação |
| POST | `/transacoes` | Cria uma transação. Com `?repetir=12`, cria a mesma transação em 12 meses seguidos, de 1 a 60 (o dia 31 cai no último dia dos meses mais curtos). Com `?parcelas=10`, divide o `valor` total em 10 parcelas mensais numeradas (de 2 a 60) |
| PUT | `/transacoes/{id}` | Edita uma transação. Com `?seguintes=true`, aplica descrição, valor, tipo e categoria também às transações seguintes da mesma série |
| POST | `/transacoes/importar` | Importa uma lista de transações de uma vez, já como pagas (todas ou nenhuma; até 2.000). Cada item: `{ "linha", "descricao", "valor", "data", "tipo", "categoriaId" }`; um erro responde 400 com o número da linha |
| PUT | `/transacoes/{id}/pago` | Marca só a situação: `{ "pago": true }` (paga/recebida) ou `false` (pendente) |
| GET | `/pendentes?mes=AAAA-MM` | Transações pendentes até o fim do mês (inclusive as atrasadas de antes), com os totais a pagar e a receber |
| DELETE | `/transacoes/{id}` | Exclui uma transação. Com `?seguintes=true`, exclui também as seguintes da mesma série |
| GET | `/saldo` | Total de receitas, total de despesas e saldo. Com `?mes=`, também o `acumulado` (saldo de tudo até o fim do mês). Sempre traz o `saldoReal` (só o que já foi pago e recebido, de todas as datas) |
| GET | `/despesas-por-categoria` | Total de despesas de cada categoria, do maior para o menor |
| GET | `/metas?mes=AAAA-MM` | Para cada categoria com meta: a meta e o gasto do mês (o mês é obrigatório) |
| GET | `/resumo-mensal?ano=AAAA` | Receitas, despesas, saldo e acumulado de cada um dos 12 meses do ano (sem `ano`, usa o ano atual) |
| GET | `/resumo-anual?ano=AAAA` | Totais do ano e do ano anterior (no ano atual, só até o mês atual nos dois) e as despesas por categoria dos dois anos |

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
├── tests/
│   └── ControleFinanceiro.Tests/  Testes automatizados da API (xUnit)
├── scripts/                 Backup automático e restauração do banco (PowerShell)
└── frontend/                Front-end em React (os testes ficam em src/*.test.js)
    └── src/
        ├── api.js           Função que chama a API e trata os erros
        ├── formatar.js      Formatação de valores (R$) e datas
        ├── csv.js           Geração e download do arquivo CSV
        ├── lerExtrato.js    Leitura de extratos CSV (separador, números, datas, colunas)
        ├── Importar.jsx     Tela de importação de extrato
        ├── App.jsx          Cabeçalho, abas (Visão geral, Transações, Categorias), filtro por mês e cartões de saldo
        ├── Login.jsx        Tela de entrar e criar conta
        ├── TrocarSenha.jsx  Formulário de troca de senha
        ├── BotaoTema.jsx    Botão de tema claro/escuro
        ├── ResumoMensal.jsx Resumo mês a mês (gráfico e tabela)
        ├── ComparacaoAnual.jsx Comparação do ano com o anterior
        ├── GraficoAcumulado.jsx  Gráfico de linha da evolução do saldo acumulado
        ├── escalaAcumulado.js    Cálculo das posições dos pontos desse gráfico
        ├── MetasDoMes.jsx   Progresso das metas de gasto do mês
        ├── ContasDoMes.jsx  Contas pendentes e atrasadas
        ├── DespesasPorCategoria.jsx  Gráfico de despesas por categoria
        ├── Transacoes.jsx   Tela de transações
        └── Categorias.jsx   Tela de categorias
```

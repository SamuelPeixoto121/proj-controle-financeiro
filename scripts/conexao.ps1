<#
  Funções usadas pelos scripts de backup e de restauração (não é para rodar sozinho).
#>

# Pasta padrão dos backups: dentro do OneDrive (vai para a nuvem); sem OneDrive, a pasta "backups" do projeto.
function PastaPadraoDeBackup {
  if ($env:OneDrive -and (Test-Path $env:OneDrive)) { return Join-Path $env:OneDrive 'Backups\ControleFinanceiro' }
  return Join-Path (Split-Path $PSScriptRoot -Parent) 'backups'
}

# Lê a conexão dos User Secrets da API (o mesmo lugar que o "dotnet user-secrets set" grava).
function LerConexao {
  $csproj = Join-Path (Split-Path $PSScriptRoot -Parent) 'backend\ControleFinanceiro.csproj'
  $id = ([xml](Get-Content $csproj -Raw)).Project.PropertyGroup.UserSecretsId | Where-Object { $_ } | Select-Object -First 1
  $arquivo = Join-Path $env:APPDATA "Microsoft\UserSecrets\$id\secrets.json"
  if (-not (Test-Path $arquivo)) { throw 'Conexão não configurada. Veja o passo 2 do README (dotnet user-secrets set ...).' }
  $texto = (Get-Content $arquivo -Raw -Encoding UTF8 | ConvertFrom-Json).'ConnectionStrings:DefaultConnection'
  if (-not $texto) { throw 'Conexão não configurada. Veja o passo 2 do README (dotnet user-secrets set ...).' }

  # "Server=localhost;Port=3306;Database=...;User=root;Password=..." -> partes
  $partes = @{}
  foreach ($item in $texto.Split(';')) {
    $par = $item.Split('=', 2)
    if ($par.Count -eq 2) { $partes[$par[0].Trim().ToLower()] = $par[1].Trim() }
  }
  $usuario = if ($partes['user']) { $partes['user'] } elseif ($partes['user id']) { $partes['user id'] } else { $partes['uid'] }
  return [pscustomobject]@{
    Servidor = if ($partes['server']) { $partes['server'] } else { 'localhost' }
    Porta    = if ($partes['port']) { $partes['port'] } else { '3306' }
    Usuario  = $usuario
    Senha    = if ($partes['password']) { $partes['password'] } else { $partes['pwd'] }
    Banco    = $partes['database']
  }
}

# A senha vai num arquivo temporário de opções (só o seu usuário lê a pasta TEMP), e não na linha de comando.
# Quem chama apaga a pasta temporária no fim.
function ArquivoDeOpcoes($conexao, [string]$pasta) {
  $arquivo = Join-Path $pasta 'opcoes.cnf'
  $senha = $conexao.Senha.Replace('\', '\\').Replace('"', '\"')
  $conteudo = "[client]`nhost=$($conexao.Servidor)`nport=$($conexao.Porta)`nuser=$($conexao.Usuario)`npassword=`"$senha`"`n"
  [IO.File]::WriteAllText($arquivo, $conteudo, (New-Object Text.UTF8Encoding $false))
  return $arquivo
}

# Caminho do mysqldump / mysql: o que estiver no PATH ou o da instalação padrão do MySQL.
function Programa([string]$nome) {
  $noPath = Get-Command "$nome.exe" -ErrorAction SilentlyContinue
  if ($noPath) { return $noPath.Source }
  $instalado = Get-ChildItem "$env:ProgramFiles\MySQL\MySQL Server *\bin\$nome.exe" -ErrorAction SilentlyContinue |
    Sort-Object FullName -Descending | Select-Object -First 1
  if ($instalado) { return $instalado.FullName }
  throw "Não encontrei o $nome.exe. Ele vem com o MySQL Server (pasta bin)."
}

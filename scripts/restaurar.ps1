<#
  Restaura um backup do Controle Financeiro.

  ATENÇÃO: substitui os dados atuais do banco pelos do backup escolhido.
  Antes de restaurar o banco da API, faz um backup do estado atual (para dar para desfazer).
  De preferência, pare a API (Ctrl+C) antes de restaurar.

  Uso (na pasta do projeto):
    powershell -ExecutionPolicy Bypass -File scripts\restaurar.ps1
  Opções:
    -Arquivo <zip>   qual backup restaurar (sem isso, mostra a lista para escolher)
    -Pasta   <pasta> onde estão os backups (padrão: OneDrive\Backups\ControleFinanceiro)
    -Banco   <nome>  em qual banco restaurar (padrão: o da conexão da API)
    -SemPerguntar    não pede confirmação; só é aceito quando o banco NÃO é o da API (usado em testes)
#>
param(
  [string]$Arquivo = '',
  [string]$Pasta = '',
  [string]$Banco = '',
  [switch]$SemPerguntar
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'conexao.ps1')

$conexao = LerConexao
if (-not $Pasta) { $Pasta = PastaPadraoDeBackup }
if (-not $Banco) { $Banco = $conexao.Banco }
$bancoDaApi = ($Banco -eq $conexao.Banco)
if ($SemPerguntar -and $bancoDaApi) { throw 'Por segurança, -SemPerguntar não pode ser usado no banco da API.' }

# 1. Escolher o backup
if (-not $Arquivo) {
  $backups = @(Get-ChildItem -Path $Pasta -Filter '*.zip' -ErrorAction SilentlyContinue | Sort-Object Name -Descending)
  if ($backups.Count -eq 0) { throw "Nenhum backup encontrado em $Pasta" }
  Write-Host "Backups em ${Pasta}:"
  for ($i = 0; $i -lt [Math]::Min($backups.Count, 30); $i++) {
    Write-Host ("  {0,2}) {1}" -f ($i + 1), $backups[$i].Name)
  }
  $escolha = Read-Host 'Número do backup para restaurar (Enter = 1, o mais recente)'
  if (-not $escolha) { $escolha = '1' }
  $Arquivo = $backups[[int]$escolha - 1].FullName
}
if (-not (Test-Path $Arquivo)) { throw "Arquivo não encontrado: $Arquivo" }

# 2. Confirmar
if (-not $SemPerguntar) {
  Write-Host ''
  Write-Host "Vai restaurar: $(Split-Path $Arquivo -Leaf)" -ForegroundColor Yellow
  Write-Host "No banco:      $Banco (os dados atuais serão substituídos)" -ForegroundColor Yellow
  if ((Read-Host 'Digite RESTAURAR para continuar') -cne 'RESTAURAR') { Write-Host 'Cancelado. Nada foi alterado.'; exit 0 }
}

$temporaria = Join-Path ([IO.Path]::GetTempPath()) "restaurar-cf-$([guid]::NewGuid())"
New-Item -ItemType Directory -Path $temporaria | Out-Null
try {
  # 3. Abre o backup ANTES de fazer o backup de segurança
  #    (assim a limpeza dos antigos não apaga justamente o arquivo escolhido).
  Expand-Archive -Path $Arquivo -DestinationPath $temporaria
  $sql = Get-ChildItem -Path $temporaria -Filter '*.sql' | Select-Object -First 1
  if (-not $sql) { throw 'Esse .zip não tem um arquivo .sql dentro.' }

  # 4. Backup de segurança do estado atual (só para o banco da API)
  if ($bancoDaApi) {
    Write-Host 'Fazendo um backup do estado atual antes de restaurar...'
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'backup.ps1') -Pasta $Pasta
    if ($LASTEXITCODE -ne 0) { throw 'O backup de segurança falhou; a restauração foi cancelada e nada foi alterado.' }
  }

  # 5. Restaura: cria o banco se não existir e executa o .sql (que recria cada tabela com os dados)
  $opcoes = ArquivoDeOpcoes $conexao $temporaria
  $mysql = Programa 'mysql'
  & $mysql "--defaults-extra-file=$opcoes" -e "CREATE DATABASE IF NOT EXISTS ``$Banco`` CHARACTER SET utf8mb4;"
  if ($LASTEXITCODE -ne 0) { throw 'Não foi possível criar/acessar o banco.' }
  $processo = Start-Process -FilePath $mysql -NoNewWindow -Wait -PassThru `
    -ArgumentList "`"--defaults-extra-file=$opcoes`"", '--default-character-set=utf8mb4', $Banco `
    -RedirectStandardInput $sql.FullName
  if ($processo.ExitCode -ne 0) { throw "O mysql terminou com erro (código $($processo.ExitCode))." }

  Write-Host "Restaurado: $(Split-Path $Arquivo -Leaf) -> banco $Banco" -ForegroundColor Green
}
finally {
  Remove-Item -Recurse -Force $temporaria -ErrorAction SilentlyContinue
}

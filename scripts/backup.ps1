<#
  Backup do banco do Controle Financeiro.

  Gera uma cópia completa do banco com o mysqldump (tabelas + dados), compactada em .zip,
  e guarda só os backups mais recentes. A senha é lida dos User Secrets da API e nunca
  aparece na linha de comando nem no log.

  Uso (na pasta do projeto):
    powershell -ExecutionPolicy Bypass -File scripts\backup.ps1
  Opções:
    -Pasta  <pasta>   onde guardar (padrão: OneDrive\Backups\ControleFinanceiro)
    -Manter <número>  quantos backups guardar (padrão: 30)
    -Banco  <nome>    qual banco copiar (padrão: o da conexão da API)
#>
param(
  [string]$Pasta = '',
  [int]$Manter = 30,
  [string]$Banco = ''
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'conexao.ps1')

if (-not $Pasta) { $Pasta = PastaPadraoDeBackup }
New-Item -ItemType Directory -Force -Path $Pasta | Out-Null
$log = Join-Path $Pasta 'backup.log'
function Registrar([string]$texto) {
  $linha = "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')  $texto"
  Write-Host $linha
  Add-Content -Path $log -Value $linha -Encoding UTF8
}

$conexao = LerConexao
if (-not $Banco) { $Banco = $conexao.Banco }
$nome = "{0}_{1}" -f $Banco, (Get-Date -Format 'yyyy-MM-dd_HHmmss')
$temporaria = Join-Path ([IO.Path]::GetTempPath()) "backup-cf-$([guid]::NewGuid())"
New-Item -ItemType Directory -Path $temporaria | Out-Null
$sql = Join-Path $temporaria "$nome.sql"
$zip = Join-Path $Pasta "$nome.zip"

try {
  # 1. Copia o banco para um arquivo .sql.
  #    --single-transaction: cópia consistente sem travar o app; --result-file: grava em UTF-8 sem passar pelo PowerShell.
  $opcoes = ArquivoDeOpcoes $conexao $temporaria
  # Avisos do mysqldump não devem interromper o script; o resultado é conferido pelo código de saída.
  $ErrorActionPreference = 'Continue'
  & (Programa 'mysqldump') "--defaults-extra-file=$opcoes" --single-transaction --routines --triggers `
    --no-tablespaces --set-gtid-purged=OFF --default-character-set=utf8mb4 "--result-file=$sql" $Banco 2>&1 |
    ForEach-Object { Registrar "mysqldump: $_" }
  $codigo = $LASTEXITCODE
  $ErrorActionPreference = 'Stop'
  if ($codigo -ne 0) { throw "o mysqldump terminou com erro (código $codigo)" }

  # 2. Confere se a cópia chegou até o fim (o mysqldump escreve "Dump completed" na última linha).
  $final = Get-Content -Path $sql -Tail 1 -Encoding UTF8
  if ($final -notmatch 'Dump completed') { throw 'o arquivo do backup ficou incompleto' }

  # 3. Compacta e guarda.
  Compress-Archive -Path $sql -DestinationPath $zip -Force
  Registrar ("OK  {0}  ({1:N0} KB)" -f (Split-Path $zip -Leaf), ((Get-Item $zip).Length / 1KB))

  # 4. Apaga os mais antigos, deixando só os $Manter mais recentes deste banco.
  Get-ChildItem -Path $Pasta -Filter "${Banco}_*.zip" |
    Sort-Object Name -Descending |
    Select-Object -Skip $Manter |
    ForEach-Object { Remove-Item $_.FullName; Registrar "apagado (antigo): $($_.Name)" }
}
catch {
  Registrar "ERRO no backup: $($_.Exception.Message)"
  exit 1
}
finally {
  Remove-Item -Recurse -Force $temporaria -ErrorAction SilentlyContinue
}

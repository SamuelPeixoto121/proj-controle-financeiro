<#
  Agenda o backup automático no Agendador de Tarefas do Windows (todo dia, no horário escolhido).
  Se o computador estiver desligado no horário, o backup roda assim que ele for ligado.
  Não precisa ser administrador.

  Uso (na pasta do projeto):
    powershell -ExecutionPolicy Bypass -File scripts\agendar-backup.ps1            (todo dia às 21:00)
    powershell -ExecutionPolicy Bypass -File scripts\agendar-backup.ps1 -Hora 08:30
    powershell -ExecutionPolicy Bypass -File scripts\agendar-backup.ps1 -Remover   (desliga o backup automático)
#>
param(
  [string]$Hora = '21:00',
  [switch]$Remover
)

$ErrorActionPreference = 'Stop'
$nome = 'Backup Controle Financeiro'

if ($Remover) {
  Unregister-ScheduledTask -TaskName $nome -Confirm:$false
  Write-Host "Tarefa '$nome' removida. O backup automático está desligado."
  exit 0
}

$script = Join-Path $PSScriptRoot 'backup.ps1'
$acao = New-ScheduledTaskAction -Execute 'powershell.exe' `
  -Argument "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File `"$script`""
$gatilho = New-ScheduledTaskTrigger -Daily -At $Hora
# StartWhenAvailable: se o PC estava desligado no horário, roda quando ligar.
$configuracao = New-ScheduledTaskSettingsSet -StartWhenAvailable -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries `
  -ExecutionTimeLimit (New-TimeSpan -Minutes 10)

Register-ScheduledTask -TaskName $nome -Action $acao -Trigger $gatilho -Settings $configuracao -Force `
  -Description 'Backup diário do banco do Controle Financeiro (scripts\backup.ps1).' | Out-Null

$info = Get-ScheduledTaskInfo -TaskName $nome
Write-Host "Tarefa '$nome' agendada: todo dia às $Hora."
Write-Host "Próxima execução: $($info.NextRunTime)"

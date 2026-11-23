import { useState } from 'react'

// Tema em uso: o escolhido no botão ou, se nenhum foi escolhido, o do sistema.
function temaAtual() {
  const escolhido = document.documentElement.dataset.theme
  if (escolhido) return escolhido
  return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light'
}

// Aplica o tema (o CSS lê o atributo data-theme do <html>) e guarda a escolha.
// O index.html lê essa escolha antes de a página aparecer, para não piscar.
function aplicarTema(tema) {
  document.documentElement.dataset.theme = tema
  try {
    localStorage.setItem('tema', tema)
  } catch {
    // Sem acesso ao armazenamento: o tema vale só até recarregar a página.
  }
}

// Botão 🌙 / ☀️ que alterna entre o tema escuro e o claro.
function BotaoTema() {
  const [tema, setTema] = useState(temaAtual)

  function trocar() {
    const novo = tema === 'dark' ? 'light' : 'dark'
    aplicarTema(novo)
    setTema(novo)
  }

  const descricao = tema === 'dark' ? 'Usar tema claro' : 'Usar tema escuro'
  return (
    <button type="button" className="botao-tema" onClick={trocar} aria-label={descricao} title={descricao}>
      {tema === 'dark' ? '☀️' : '🌙'}
    </button>
  )
}

export default BotaoTema

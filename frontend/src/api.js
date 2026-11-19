const API_URL = 'http://localhost:5083'

// Chama a API e devolve o JSON da resposta.
// Se a API responder com erro, lança um Error com a mensagem dela.
export async function api(caminho, opcoes = {}) {
  let resposta
  try {
    resposta = await fetch(`${API_URL}${caminho}`, {
      method: opcoes.method ?? 'GET',
      headers: opcoes.body ? { 'Content-Type': 'application/json' } : undefined,
      body: opcoes.body ? JSON.stringify(opcoes.body) : undefined,
      credentials: 'include', // envia o cookie de login
    })
  } catch {
    throw new Error('Não foi possível conectar à API. Ela está rodando?')
  }

  // Sessão expirada: avisa o App para voltar à tela de login.
  if (resposta.status === 401 && !caminho.startsWith('/auth/')) {
    window.dispatchEvent(new Event('sessao-expirada'))
    throw new Error('Sua sessão expirou. Entre novamente.')
  }

  if (resposta.status === 204) return null

  const dados = await resposta.json().catch(() => null)
  if (!resposta.ok) {
    throw new Error(typeof dados === 'string' ? dados : `Erro ${resposta.status}`)
  }
  return dados
}

import { useEffect, useState } from 'react'
import { api } from './api.js'
import BotaoTema from './BotaoTema.jsx'
import { formatarMes, formatarReais, mesAtual } from './formatar.js'
import Categorias from './Categorias.jsx'
import Contas from './Contas.jsx'
import ContasDoMes from './ContasDoMes.jsx'
import DespesasPorCategoria from './DespesasPorCategoria.jsx'
import Importar from './Importar.jsx'
import Login from './Login.jsx'
import MetaEconomia from './MetaEconomia.jsx'
import MetasDoMes from './MetasDoMes.jsx'
import Relatorio from './Relatorio.jsx'
import ResumoMensal from './ResumoMensal.jsx'
import Transacoes from './Transacoes.jsx'
import TrocarSenha from './TrocarSenha.jsx'

const ABAS = ['Visão geral', 'Transações', 'Contas', 'Categorias', 'Importar', 'Relatório']

// O navegador lembra a última aba aberta (se não der para ler, abre a primeira).
function abaSalva() {
  try {
    const aba = localStorage.getItem('aba')
    return ABAS.includes(aba) ? aba : ABAS[0]
  } catch {
    return ABAS[0]
  }
}

// Um dos cartões grandes da visão geral (Receitas, Despesas...).
function Cartao({ titulo, valor, classe, detalhe }) {
  return (
    <div className="cartao">
      <span className="cartao-titulo">{titulo}</span>
      <strong className={`cartao-valor ${classe}`}>{formatarReais(valor)}</strong>
      {detalhe && <small className="cartao-detalhe">{detalhe}</small>}
    </div>
  )
}

const corDoSaldo = (valor) => (valor < 0 ? 'despesa' : 'receita')

function App() {
  // undefined = ainda verificando, null = ninguém logado, objeto = usuário logado
  const [usuario, setUsuario] = useState(undefined)
  const [saldo, setSaldo] = useState(null)
  const [erro, setErro] = useState('')
  // Sobe a cada alteração, para o saldo e as listas recarregarem.
  const [atualizacao, setAtualizacao] = useState(0)
  const aoAlterar = () => setAtualizacao((n) => n + 1)
  // Mês escolhido ("2026-11"). Vazio = todos os meses.
  const [mes, setMes] = useState(mesAtual)
  const [aba, setAba] = useState(abaSalva)
  // Formulário de troca de senha aberto? E o aviso "Senha alterada."
  const [trocandoSenha, setTrocandoSenha] = useState(false)
  const [aviso, setAviso] = useState('')

  // Ao abrir a página: já existe alguém logado?
  useEffect(() => {
    api('/auth/eu')
      .then(setUsuario)
      .catch(() => setUsuario(null))
  }, [])

  // Se a sessão expirar no meio do uso, volta para a tela de login.
  useEffect(() => {
    const voltarAoLogin = () => setUsuario(null)
    window.addEventListener('sessao-expirada', voltarAoLogin)
    return () => window.removeEventListener('sessao-expirada', voltarAoLogin)
  }, [])

  useEffect(() => {
    if (!usuario) return
    api(mes ? `/saldo?mes=${mes}` : '/saldo')
      .then(setSaldo)
      .catch((e) => setErro(e.message))
  }, [usuario, atualizacao, mes])

  function trocarAba(nova) {
    setAba(nova)
    try {
      localStorage.setItem('aba', nova)
    } catch {
      // Sem acesso ao armazenamento do navegador: só não lembra a aba.
    }
  }

  async function sair() {
    await api('/auth/logout', { method: 'POST' }).catch(() => {})
    setSaldo(null)
    setErro('')
    setTrocandoSenha(false)
    setAviso('')
    setUsuario(null)
  }

  if (usuario === undefined) return <p className="carregando">Carregando...</p>
  if (usuario === null) return <Login aoEntrar={setUsuario} />

  return (
    <>
      <header className="cabecalho">
        <div className="cabecalho-conteudo">
          <h1>💰 Controle Financeiro</h1>
          <span className="usuario">{usuario.email}</span>
          <BotaoTema />
          <button
            onClick={() => {
              setTrocandoSenha(!trocandoSenha)
              setAviso('')
            }}
          >
            Trocar senha
          </button>
          <button onClick={sair}>Sair</button>
        </div>
      </header>

      <main>
        {trocandoSenha && (
          <TrocarSenha
            aoTrocar={() => {
              setTrocandoSenha(false)
              setAviso('Senha alterada.')
            }}
            aoCancelar={() => setTrocandoSenha(false)}
          />
        )}
        {aviso && <p className="aviso">{aviso}</p>}

        <nav className="abas" role="tablist">
          {ABAS.map((nome) => (
            <button
              key={nome}
              role="tab"
              aria-selected={aba === nome}
              className={aba === nome ? 'aba ativa' : 'aba'}
              onClick={() => trocarAba(nome)}
            >
              {nome}
            </button>
          ))}
        </nav>

        {(aba === 'Visão geral' || aba === 'Transações') && (
          <div className="filtro-mes">
            <label htmlFor="mes">Mês</label>
            <input id="mes" type="month" value={mes} onChange={(e) => setMes(e.target.value)} />
            <button onClick={() => setMes('')} disabled={!mes}>
              Todos os meses
            </button>
            {!mes && <span className="texto-suave">Mostrando todos os meses</span>}
          </div>
        )}

        {aba === 'Visão geral' && (
          <>
            {erro && <p className="erro">{erro}</p>}
            {!erro && !saldo && <p className="carregando">Carregando...</p>}

            {saldo && (
              <div className="cartoes">
                <Cartao titulo="Receitas" valor={saldo.receitas} classe="receita" />
                <Cartao titulo="Despesas" valor={saldo.despesas} classe="despesa" />
                <Cartao
                  titulo={mes ? 'Saldo do mês' : 'Saldo'}
                  valor={saldo.saldo}
                  classe={corDoSaldo(saldo.saldo)}
                  detalhe="Receitas − despesas"
                />
                {mes && saldo.acumulado !== undefined && (
                  <Cartao
                    titulo="Acumulado"
                    valor={saldo.acumulado}
                    classe={corDoSaldo(saldo.acumulado)}
                    detalhe={`Previsão para o fim de ${formatarMes(mes)}`}
                  />
                )}
                <Cartao
                  titulo="Saldo real"
                  valor={saldo.saldoReal}
                  classe={corDoSaldo(saldo.saldoReal)}
                  detalhe="Soma das contas: só o que já foi pago e recebido"
                />
              </div>
            )}

            {mes && saldo && (
              <MetaEconomia mes={mes} saldoDoMes={saldo.saldo} atualizacao={atualizacao} aoAlterar={aoAlterar} />
            )}
            <ContasDoMes
              mes={mes}
              atualizacao={atualizacao}
              aoAlterar={aoAlterar}
              saldoReal={saldo?.saldoReal}
            />
            <div className="grade-2">
              <MetasDoMes mes={mes} atualizacao={atualizacao} />
              <DespesasPorCategoria mes={mes} atualizacao={atualizacao} />
            </div>
            <ResumoMensal mes={mes} atualizacao={atualizacao} />
          </>
        )}

        {aba === 'Transações' && (
          <Transacoes mes={mes} atualizacao={atualizacao} aoAlterar={aoAlterar} />
        )}

        {aba === 'Contas' && <Contas atualizacao={atualizacao} aoAlterar={aoAlterar} />}

        {aba === 'Categorias' && <Categorias aoAlterar={aoAlterar} />}

        {aba === 'Importar' && <Importar aoAlterar={aoAlterar} />}

        {aba === 'Relatório' && <Relatorio email={usuario.email} atualizacao={atualizacao} />}
      </main>
    </>
  )
}

export default App

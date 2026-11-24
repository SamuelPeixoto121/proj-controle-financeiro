import { useEffect, useState } from 'react'
import { api } from './api.js'
import BarraEconomia from './BarraEconomia.jsx'
import { NOMES_MESES, formatarData, formatarReais, mesAtual, situacaoDe, variacao } from './formatar.js'
import { despesasPorCategoria, mesPorExtenso, totaisDe, transacoesDoAno } from './relatorio.js'

const corDoValor = (valor) => (valor < 0 ? 'despesa' : valor > 0 ? 'receita' : '')

// Linha "rótulo .... valor" do resumo.
function Item({ rotulo, valor, colorir = false, detalhe }) {
  return (
    <div>
      <dt>{rotulo}</dt>
      <dd className={colorir ? corDoValor(valor) : ''}>
        {formatarReais(valor)}
        {detalhe && <small>{detalhe}</small>}
      </dd>
    </div>
  )
}

// Variação em relação ao ano anterior, em texto (para o papel, sem depender de cor).
// emReais: a diferença em R$ (para o saldo, que pode ser negativo e não combina com %).
function textoVariacao(atual, anterior, emReais = false) {
  const diferenca = Math.round((atual - anterior) * 100) / 100
  if (diferenca === 0) return 'igual'
  const seta = diferenca > 0 ? '▲' : '▼'
  if (emReais) return `${seta} ${formatarReais(Math.abs(diferenca))}`
  const pct = variacao(atual, anterior)
  if (pct === null) return 'novo'
  if (pct === 0) return '≈ igual' // diferença menor que 0,5%
  return `${seta} ${Math.abs(pct)}%`
}

function TabelaCategorias({ categorias, metas = [] }) {
  if (categorias.length === 0) return <p className="vazio">Nenhuma despesa.</p>
  const temMeta = metas.length > 0
  return (
    <table className="tabela-relatorio">
      <thead>
        <tr>
          <th>Categoria</th>
          <th className="valor">Valor</th>
          <th className="valor">% das despesas</th>
          {temMeta && <th className="valor">Meta do mês</th>}
        </tr>
      </thead>
      <tbody>
        {categorias.map((c) => {
          const meta = metas.find((m) => m.categoria === c.categoria)
          return (
            <tr key={c.categoria}>
              <td>{c.categoria}</td>
              <td className="valor">{formatarReais(c.valor)}</td>
              <td className="valor">{c.porcentagem.toLocaleString('pt-BR')}%</td>
              {temMeta && (
                <td className="valor">
                  {meta ? (
                    <>
                      {formatarReais(meta.meta)}{' '}
                      {meta.gasto <= meta.meta ? (
                        <span className="meta-icone dentro">✔ dentro</span>
                      ) : (
                        <span className="meta-icone estourou">✖ estourou</span>
                      )}
                    </>
                  ) : (
                    '—'
                  )}
                </td>
              )}
            </tr>
          )
        })}
      </tbody>
    </table>
  )
}

function TabelaTransacoes({ transacoes, mostrarConta }) {
  if (transacoes.length === 0) return <p className="vazio">Nenhuma transação.</p>
  const { receitas, despesas } = totaisDe(transacoes)
  return (
    <table className="tabela-relatorio">
      <thead>
        <tr>
          <th>Data</th>
          <th>Descrição</th>
          <th>Categoria</th>
          {mostrarConta && <th>Conta</th>}
          <th>Situação</th>
          <th className="valor">Valor</th>
        </tr>
      </thead>
      <tbody>
        {transacoes.map((t) => (
          <tr key={t.id}>
            <td>{formatarData(t.data)}</td>
            <td>
              {t.descricao}
              {t.cartao && <small className="compra-cartao">💳 {t.cartao.nome}</small>}
            </td>
            <td>{t.categoria?.nome}</td>
            {mostrarConta && <td>{t.conta?.nome}</td>}
            <td>{situacaoDe(t)}</td>
            <td className={`valor ${t.tipo === 'Receita' ? 'receita' : 'despesa'}`}>
              {t.tipo === 'Receita' ? '+ ' : '− '}
              {formatarReais(t.valor)}
            </td>
          </tr>
        ))}
      </tbody>
      <tfoot>
        <tr>
          <th colSpan={mostrarConta ? 5 : 4}>
            {transacoes.length} {transacoes.length === 1 ? 'transação' : 'transações'}
          </th>
          <th className="valor">
            <span className="receita">+ {formatarReais(receitas)}</span>
            <br />
            <span className="despesa">− {formatarReais(despesas)}</span>
          </th>
        </tr>
      </tfoot>
    </table>
  )
}

function TabelaContas({ contas }) {
  const total = contas.reduce((soma, c) => soma + c.saldo, 0)
  return (
    <table className="tabela-relatorio">
      <thead>
        <tr>
          <th>Conta</th>
          <th className="valor">Saldo hoje</th>
        </tr>
      </thead>
      <tbody>
        {contas.map((c) => (
          <tr key={c.id}>
            <td>{c.nome}</td>
            <td className={`valor ${corDoValor(c.saldo)}`}>{formatarReais(c.saldo)}</td>
          </tr>
        ))}
      </tbody>
      {contas.length > 1 && (
        <tfoot>
          <tr>
            <th>Total</th>
            <th className={`valor ${corDoValor(total)}`}>{formatarReais(total)}</th>
          </tr>
        </tfoot>
      )}
    </table>
  )
}

function RelatorioDoMes({ mes, d }) {
  const categorias = despesasPorCategoria(d.transacoes)
  const transacoes = [...d.transacoes].sort((a, b) => a.data.localeCompare(b.data) || a.id - b.id)
  return (
    <>
      <section className="relatorio-bloco">
        <h3>Resumo</h3>
        <dl className="relatorio-resumo">
          <Item rotulo="Receitas" valor={d.saldo.receitas} />
          <Item rotulo="Despesas" valor={d.saldo.despesas} />
          <Item rotulo="Saldo do mês" valor={d.saldo.saldo} colorir detalhe="receitas − despesas" />
          <Item rotulo="Acumulado no fim do mês" valor={d.saldo.acumulado} colorir detalhe="previsão" />
          <Item rotulo="Saldo real hoje" valor={d.saldo.saldoReal} colorir detalhe="só o que já foi pago e recebido" />
        </dl>
      </section>

      {d.metaMensal != null && (
        <section className="relatorio-bloco">
          <h3>Meta de economia</h3>
          <BarraEconomia rotulo="Guardado no mês" saldo={d.saldo.saldo} meta={d.metaMensal} />
        </section>
      )}

      <section className="relatorio-bloco">
        <h3>Despesas por categoria</h3>
        <TabelaCategorias categorias={categorias} metas={d.metas} />
      </section>

      <section className="relatorio-bloco">
        <h3>Contas</h3>
        <TabelaContas contas={d.contas} />
      </section>

      {d.pendentes.transacoes.length > 0 && (
        <section className="relatorio-bloco">
          <h3>Contas pendentes</h3>
          <p className="texto-suave">
            A pagar: {formatarReais(d.pendentes.aPagar)} · A receber: {formatarReais(d.pendentes.aReceber)} (inclui as
            atrasadas de meses anteriores)
          </p>
          <table className="tabela-relatorio">
            <thead>
              <tr>
                <th>Vencimento</th>
                <th>Descrição</th>
                <th>Tipo</th>
                <th className="valor">Valor</th>
              </tr>
            </thead>
            <tbody>
              {d.pendentes.transacoes.map((t) => (
                <tr key={t.id}>
                  <td>
                    {formatarData(t.data)}
                    {t.atrasada && <strong className="despesa"> ⚠ atrasada</strong>}
                  </td>
                  <td>{t.cartao ? `💳 ${t.descricao} (${t.cartao})` : t.descricao}</td>
                  <td>{t.tipo === 'Receita' ? 'a receber' : 'a pagar'}</td>
                  <td className="valor">{formatarReais(t.valor)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </section>
      )}

      <section className="relatorio-bloco">
        <h3>Transações de {mesPorExtenso(mes)}</h3>
        <TabelaTransacoes transacoes={transacoes} mostrarConta={d.contas.length > 1} />
      </section>
    </>
  )
}

function RelatorioDoAno({ ano, d, incluirTransacoes }) {
  const doAno = transacoesDoAno(d.transacoes, ano)
  const total = (campo) => d.meses.reduce((soma, m) => soma + m[campo], 0)
  const saldoDoAno = total('saldo')
  const { atual, anterior, ateMes } = d.anual
  return (
    <>
      <section className="relatorio-bloco">
        <h3>Resumo do ano</h3>
        <dl className="relatorio-resumo">
          <Item rotulo="Receitas" valor={total('receitas')} />
          <Item rotulo="Despesas" valor={total('despesas')} />
          <Item rotulo="Saldo do ano" valor={saldoDoAno} colorir detalhe="receitas − despesas" />
          <Item rotulo="Acumulado no fim do ano" valor={d.meses.at(-1).acumulado} colorir detalhe="previsão" />
        </dl>
      </section>

      <section className="relatorio-bloco">
        <h3>
          Comparação com {ano - 1}
          {ateMes < 12 && ` (de jan a ${NOMES_MESES[ateMes - 1]} nos dois anos)`}
        </h3>
        <table className="tabela-relatorio">
          <thead>
            <tr>
              <th></th>
              <th className="valor">{ano}</th>
              <th className="valor">{ano - 1}</th>
              <th className="valor">Variação</th>
            </tr>
          </thead>
          <tbody>
            {[
              ['Receitas', 'receitas'],
              ['Despesas', 'despesas'],
              ['Saldo', 'saldo'],
            ].map(([rotulo, campo]) => (
              <tr key={campo}>
                <td>{rotulo}</td>
                <td className="valor">{formatarReais(atual[campo])}</td>
                <td className="valor">{formatarReais(anterior[campo])}</td>
                <td className="valor">{textoVariacao(atual[campo], anterior[campo], campo === 'saldo')}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </section>

      {d.metaMensal != null && (
        <section className="relatorio-bloco">
          <h3>Meta de economia do ano</h3>
          <BarraEconomia rotulo={`Previsão de guardar em ${ano}`} saldo={saldoDoAno} meta={d.metaMensal * 12} />
        </section>
      )}

      <section className="relatorio-bloco">
        <h3>Mês a mês</h3>
        <table className="tabela-relatorio">
          <thead>
            <tr>
              <th>Mês</th>
              <th className="valor">Receitas</th>
              <th className="valor">Despesas</th>
              <th className="valor">Saldo</th>
              <th className="valor">Acumulado</th>
            </tr>
          </thead>
          <tbody>
            {d.meses.map((m, i) => (
              <tr key={m.mes}>
                <td>
                  {NOMES_MESES[i]}/{ano}
                </td>
                <td className="valor">{formatarReais(m.receitas)}</td>
                <td className="valor">{formatarReais(m.despesas)}</td>
                <td className={`valor ${corDoValor(m.saldo)}`}>
                  {d.metaMensal != null && m.saldo >= d.metaMensal && (
                    <span className="meta-icone dentro" title="Bateu a meta de economia">
                      ✔{' '}
                    </span>
                  )}
                  {formatarReais(m.saldo)}
                </td>
                <td className={`valor ${corDoValor(m.acumulado)}`}>{formatarReais(m.acumulado)}</td>
              </tr>
            ))}
          </tbody>
          <tfoot>
            <tr>
              <th>Total</th>
              <th className="valor">{formatarReais(total('receitas'))}</th>
              <th className="valor">{formatarReais(total('despesas'))}</th>
              <th className={`valor ${corDoValor(saldoDoAno)}`}>{formatarReais(saldoDoAno)}</th>
              <th></th>
            </tr>
          </tfoot>
        </table>
        {d.metaMensal != null && (
          <small className="texto-suave">✔ = mês que bateu a meta de {formatarReais(d.metaMensal)}.</small>
        )}
      </section>

      <section className="relatorio-bloco">
        <h3>Despesas por categoria em {ano}</h3>
        <TabelaCategorias categorias={despesasPorCategoria(doAno)} />
      </section>

      {incluirTransacoes && (
        <section className="relatorio-bloco">
          <h3>Transações de {ano}</h3>
          <TabelaTransacoes transacoes={doAno} mostrarConta={d.contas.length > 1} />
        </section>
      )}
    </>
  )
}

// Aba Relatório: o relatório do mês ou do ano, pronto para imprimir ou salvar em PDF.
function Relatorio({ email, atualizacao }) {
  const [tipo, setTipo] = useState('mes')
  const [mes, setMes] = useState(mesAtual)
  const [ano, setAno] = useState(() => Number(mesAtual().slice(0, 4)))
  const [incluirTransacoes, setIncluirTransacoes] = useState(false)
  const [dados, setDados] = useState(null)
  const [erro, setErro] = useState('')

  // Qual relatório está na tela (para não mostrar dados de outro enquanto carrega).
  const chave = tipo === 'mes' ? `mes:${mes}` : `ano:${ano}`

  useEffect(() => {
    if (tipo === 'mes' && !mes) return
    const pedidos =
      tipo === 'mes'
        ? {
            saldo: `/saldo?mes=${mes}`,
            transacoes: `/transacoes?mes=${mes}`,
            metas: `/metas?mes=${mes}`,
            pendentes: `/pendentes?mes=${mes}`,
          }
        : { meses: `/resumo-mensal?ano=${ano}`, anual: `/resumo-anual?ano=${ano}`, transacoes: '/transacoes' }
    pedidos.contas = '/contas'
    pedidos.meta = '/meta-economia'
    const nomes = Object.keys(pedidos)
    Promise.all(nomes.map((n) => api(pedidos[n])))
      .then((respostas) => {
        const d = Object.fromEntries(nomes.map((n, i) => [n, respostas[i]]))
        // "Gerado em": a hora em que os dados chegaram.
        const agora = new Date()
        const geradoEm = `${agora.toLocaleDateString('pt-BR')} às ${agora.toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' })}`
        setDados({ ...d, metaMensal: d.meta.metaMensal, geradoEm, chave: tipo === 'mes' ? `mes:${mes}` : `ano:${ano}` })
        setErro('')
      })
      .catch((e) => setErro(e.message))
  }, [tipo, mes, ano, atualizacao])

  const pronto = dados && dados.chave === chave

  return (
    <>
      <section className="card nao-imprimir">
        <h2>Relatório</h2>
        <p className="texto-suave">
          Escolha o período e clique em Imprimir. Na janela que abrir, escolha a impressora ou “Salvar como PDF”.
        </p>
        <div className="formulario relatorio-opcoes">
          <label className="campo">
            Período
            <select value={tipo} onChange={(e) => setTipo(e.target.value)}>
              <option value="mes">Mês</option>
              <option value="ano">Ano</option>
            </select>
          </label>
          {tipo === 'mes' ? (
            <label className="campo">
              Mês
              <input type="month" value={mes} onChange={(e) => setMes(e.target.value)} required />
            </label>
          ) : (
            <label className="campo">
              Ano
              <input
                type="number"
                min="1900"
                max="2100"
                value={ano}
                onChange={(e) => setAno(Number(e.target.value))}
              />
            </label>
          )}
          {tipo === 'ano' && (
            <label className="seguintes">
              <input
                type="checkbox"
                checked={incluirTransacoes}
                onChange={(e) => setIncluirTransacoes(e.target.checked)}
              />
              Incluir a lista de todas as transações
            </label>
          )}
          <div className="formulario-botoes">
            <button className="principal" onClick={() => window.print()} disabled={!pronto}>
              🖨 Imprimir / Salvar PDF
            </button>
          </div>
        </div>
        {erro && <p className="erro">{erro}</p>}
      </section>

      {pronto && (
        <article className="card relatorio">
          <header className="relatorio-cabecalho">
            <h2>Relatório de {tipo === 'mes' ? mesPorExtenso(mes) : ano}</h2>
            <p className="texto-suave">
              💰 Controle Financeiro · {email} · gerado em {dados.geradoEm}
            </p>
          </header>
          {tipo === 'mes' ? (
            <RelatorioDoMes mes={mes} d={dados} />
          ) : (
            <RelatorioDoAno ano={ano} d={dados} incluirTransacoes={incluirTransacoes} />
          )}
        </article>
      )}
    </>
  )
}

export default Relatorio

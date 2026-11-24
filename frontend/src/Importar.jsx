import { useEffect, useState } from 'react'
import { api } from './api.js'
import { formatarData, formatarReais, normalizar } from './formatar.js'
import { decodificar, interpretarExtrato, montarTransacoes } from './lerExtrato.js'

// "Identidade" de uma transação, para achar as que já existem (mesma data, valor, tipo e descrição).
const chave = (t) =>
  `${t.data.slice(0, 10)}|${Number(t.valor).toFixed(2)}|${t.tipo}|${normalizar(t.descricao.trim())}`

// Seletor de coluna do extrato (Data, Descrição, Valor...).
function SeletorColuna({ rotulo, cabecalho, valor, aoMudar }) {
  return (
    <label className="campo">
      {rotulo}
      <select value={valor} onChange={(e) => aoMudar(Number(e.target.value))}>
        <option value={-1}>(nenhuma)</option>
        {cabecalho.map((nome, i) => (
          <option key={i} value={i}>
            {nome || `Coluna ${i + 1}`}
          </option>
        ))}
      </select>
    </label>
  )
}

// Importação de extrato CSV: escolher o arquivo, conferir as colunas, revisar e importar.
function Importar({ aoAlterar }) {
  const [categorias, setCategorias] = useState([])
  const [contas, setContas] = useState([])
  const [contaId, setContaId] = useState('') // vazio = a conta principal
  const [existentes, setExistentes] = useState(new Set())
  const [nomeArquivo, setNomeArquivo] = useState('')
  const [extrato, setExtrato] = useState(null) // { cabecalho, linhas } do arquivo
  const [colunas, setColunas] = useState(null)
  const [inverter, setInverter] = useState(false)
  const [categoriaPadrao, setCategoriaPadrao] = useState('')
  // Mudanças feitas à mão, linha a linha (a chave é o número da linha no arquivo).
  const [marcadas, setMarcadas] = useState({})
  const [categoriaDaLinha, setCategoriaDaLinha] = useState({})
  const [erro, setErro] = useState('')
  const [aviso, setAviso] = useState('')
  const [enviando, setEnviando] = useState(false)
  // Muda ao recomeçar, para o campo de arquivo ser recriado vazio.
  const [versaoCampo, setVersaoCampo] = useState(0)

  // Categorias (para escolher) e transações que já existem (para achar duplicadas).
  useEffect(() => {
    Promise.all([api('/categorias'), api('/transacoes'), api('/contas')])
      .then(([listaCategorias, listaTransacoes, listaContas]) => {
        setCategorias(listaCategorias)
        setContas(listaContas)
        setExistentes(new Set(listaTransacoes.map(chave)))
      })
      .catch((e) => setErro(e.message))
  }, [])

  // As linhas do extrato já interpretadas, com o que vale para cada uma.
  const linhas = (extrato && colunas ? montarTransacoes(extrato.linhas, colunas, inverter) : []).map((t) => {
    const duplicada = !t.erro && existentes.has(chave(t))
    return {
      ...t,
      duplicada,
      // Vem marcada se não tem problema e não é duplicada; a pessoa pode mudar.
      marcada: !t.erro && (marcadas[t.linha] ?? !duplicada),
      categoriaId: categoriaDaLinha[t.linha] ?? categoriaPadrao,
    }
  })
  const selecionadas = linhas.filter((l) => l.marcada)
  const faltaCategoria = selecionadas.some((l) => !l.categoriaId)
  const somar = (tipo) => selecionadas.filter((l) => l.tipo === tipo).reduce((s, l) => s + l.valor, 0)
  const contar = (filtro) => linhas.filter(filtro).length

  async function escolherArquivo(evento) {
    const arquivo = evento.target.files[0]
    if (!arquivo) return
    setErro('')
    setAviso('')
    if (arquivo.size > 5_000_000) {
      setErro('Arquivo grande demais (máximo 5 MB).')
      return
    }
    const lido = interpretarExtrato(decodificar(new Uint8Array(await arquivo.arrayBuffer())))
    if (lido.linhas.length === 0) {
      setErro('Não encontrei transações nesse arquivo. Confira se é um extrato em CSV.')
      return
    }
    setNomeArquivo(arquivo.name)
    setExtrato(lido)
    setColunas(lido.colunas)
    setInverter(false)
    setMarcadas({})
    setCategoriaDaLinha({})
  }

  function recomecar() {
    setVersaoCampo((v) => v + 1)
    setNomeArquivo('')
    setExtrato(null)
    setColunas(null)
    setErro('')
  }

  async function importar() {
    setErro('')
    setEnviando(true)
    try {
      const corpo = selecionadas.map((l) => ({
        linha: l.linha,
        descricao: l.descricao,
        valor: l.valor,
        data: l.data,
        tipo: l.tipo,
        categoriaId: Number(l.categoriaId),
      }))
      const resposta = await api(`/transacoes/importar${contaId ? `?contaId=${contaId}` : ''}`, { method: 'POST', body: corpo })
      const lista = await api('/transacoes')
      setExistentes(new Set(lista.map(chave)))
      recomecar()
      setAviso(`${resposta.importadas} ${resposta.importadas === 1 ? 'transação importada' : 'transações importadas'} ✔`)
      aoAlterar()
    } catch (e) {
      setErro(e.message)
    } finally {
      setEnviando(false)
    }
  }

  return (
    <>
      <section className="card">
        <h2>1. Escolher o extrato</h2>
        <p className="texto-suave">
          Exporte o extrato da conta ou a fatura do cartão em <strong>CSV</strong> no app ou site do banco e escolha o
          arquivo. Ele é lido aqui no navegador; só as transações que você confirmar são enviadas.
        </p>
        <div className="linha arquivo">
          {/* A key muda ao recomeçar, para o campo de arquivo voltar a ficar vazio */}
          <input key={versaoCampo} type="file" accept=".csv,text/csv" onChange={escolherArquivo} />
          {extrato && <button onClick={recomecar}>Escolher outro</button>}
        </div>
        {aviso && <p className="aviso">{aviso}</p>}
        {erro && <p className="erro">{erro}</p>}
        {categorias.length === 0 && (
          <p className="vazio">Antes de importar, crie pelo menos uma categoria na aba Categorias.</p>
        )}
      </section>

      {extrato && colunas && (
        <section className="card">
          <h2>2. Conferir as colunas</h2>
          <p className="texto-suave">
            Arquivo <strong>{nomeArquivo}</strong>: {extrato.linhas.length} linhas. Confira se as colunas abaixo estão
            certas; se não estiverem, escolha a coluna correta.
          </p>
          <div className="formulario">
            <SeletorColuna
              rotulo="Data"
              cabecalho={extrato.cabecalho}
              valor={colunas.data}
              aoMudar={(i) => setColunas({ ...colunas, data: i })}
            />
            <SeletorColuna
              rotulo="Descrição"
              cabecalho={extrato.cabecalho}
              valor={colunas.descricao}
              aoMudar={(i) => setColunas({ ...colunas, descricao: i })}
            />
            <SeletorColuna
              rotulo="Valor"
              cabecalho={extrato.cabecalho}
              valor={colunas.valor}
              aoMudar={(i) => setColunas({ ...colunas, valor: i })}
            />
            {colunas.valor < 0 && (
              <>
                <SeletorColuna
                  rotulo="Crédito (entradas)"
                  cabecalho={extrato.cabecalho}
                  valor={colunas.credito}
                  aoMudar={(i) => setColunas({ ...colunas, credito: i })}
                />
                <SeletorColuna
                  rotulo="Débito (saídas)"
                  cabecalho={extrato.cabecalho}
                  valor={colunas.debito}
                  aoMudar={(i) => setColunas({ ...colunas, debito: i })}
                />
              </>
            )}
            <label className="seguintes">
              <input type="checkbox" checked={inverter} onChange={(e) => setInverter(e.target.checked)} />
              É fatura de cartão (valores positivos são despesas)
            </label>
          </div>
        </section>
      )}

      {extrato && colunas && (
        <section className="card">
          <h2>3. Revisar e importar</h2>
          <p className="texto-suave">
            {contar((l) => !l.erro && !l.duplicada)} prontas · {contar((l) => l.duplicada)} já existem (vêm
            desmarcadas) · {contar((l) => l.erro)} com problema (não serão importadas)
          </p>

          <div className="categoria-padrao">
            <label className="campo">
              Categoria para todas
              <select value={categoriaPadrao} onChange={(e) => setCategoriaPadrao(e.target.value)}>
                <option value="">Escolha...</option>
                {categorias.map((c) => (
                  <option key={c.id} value={c.id}>
                    {c.nome}
                  </option>
                ))}
              </select>
            </label>
            {contas.length > 1 && (
              <label className="campo">
                Importar para a conta
                <select value={contaId || String(contas[0].id)} onChange={(e) => setContaId(e.target.value)}>
                  {contas.map((c) => (
                    <option key={c.id} value={c.id}>
                      {c.nome}
                    </option>
                  ))}
                </select>
              </label>
            )}
          </div>

          <div className="tabela-rolagem">
            <table className="tabela-importar">
              <thead>
                <tr>
                  <th></th>
                  <th>Data</th>
                  <th>Descrição</th>
                  <th className="valor">Valor</th>
                  <th>Categoria</th>
                </tr>
              </thead>
              <tbody>
                {linhas.map((l) => (
                  <tr key={l.linha} className={l.marcada ? '' : 'desmarcada'}>
                    <td>
                      <input
                        type="checkbox"
                        checked={l.marcada}
                        disabled={Boolean(l.erro)}
                        onChange={(e) => setMarcadas({ ...marcadas, [l.linha]: e.target.checked })}
                        aria-label={`Importar a linha ${l.linha}`}
                      />
                    </td>
                    <td>{l.data ? formatarData(l.data) : '—'}</td>
                    <td>
                      {l.descricao || '—'}
                      {l.erro && <small className="despesa"> ⚠ Linha {l.linha}: {l.erro}</small>}
                      {l.duplicada && <small className="texto-suave"> ⚠ já existe</small>}
                    </td>
                    <td className={`valor ${l.tipo === 'Receita' ? 'receita' : 'despesa'}`}>
                      {l.erro ? '' : `${l.tipo === 'Receita' ? '+ ' : '− '}${formatarReais(l.valor)}`}
                    </td>
                    <td>
                      {!l.erro && (
                        <select
                          value={l.categoriaId}
                          onChange={(e) => setCategoriaDaLinha({ ...categoriaDaLinha, [l.linha]: e.target.value })}
                          aria-label={`Categoria da linha ${l.linha}`}
                        >
                          <option value="">Escolha...</option>
                          {categorias.map((c) => (
                            <option key={c.id} value={c.id}>
                              {c.nome}
                            </option>
                          ))}
                        </select>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <div className="rodape-lista">
            <span>
              {selecionadas.length} selecionadas · Receitas{' '}
              <span className="receita">{formatarReais(somar('Receita'))}</span> · Despesas{' '}
              <span className="despesa">{formatarReais(somar('Despesa'))}</span>
              {faltaCategoria && <strong className="despesa"> · escolha a categoria de todas</strong>}
            </span>
            <button
              className="principal"
              onClick={importar}
              disabled={enviando || selecionadas.length === 0 || faltaCategoria}
            >
              {enviando
                ? 'Importando...'
                : `Importar ${selecionadas.length} ${selecionadas.length === 1 ? 'transação' : 'transações'}`}
            </button>
          </div>
        </section>
      )}
    </>
  )
}

export default Importar

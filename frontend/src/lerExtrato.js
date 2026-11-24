// Leitura de extratos bancários em CSV. Tudo roda no navegador.
import { normalizar } from './formatar.js'

// Bytes do arquivo -> texto. Tenta UTF-8; se não for UTF-8 válido, usa Windows-1252
// (muitos bancos brasileiros exportam assim, e sem isso "Descrição" viraria "Descri��o").
export function decodificar(bytes) {
  let texto
  try {
    texto = new TextDecoder('utf-8', { fatal: true }).decode(bytes)
  } catch {
    texto = new TextDecoder('windows-1252').decode(bytes)
  }
  return texto.replace(/^﻿/, '') // tira o BOM do Excel, se houver
}

// Separador mais provável: o que mais aparece (fora de aspas) na primeira linha.
export function detectarSeparador(texto) {
  const primeiraLinha = texto.split(/\r?\n/).find((l) => l.trim() !== '') ?? ''
  const contar = (sep) => primeiraLinha.replace(/"[^"]*"/g, '').split(sep).length - 1
  return [';', ',', '\t'].reduce((melhor, sep) => (contar(sep) > contar(melhor) ? sep : melhor), ';')
}

// Texto CSV -> lista de linhas, cada uma uma lista de campos.
// Entende aspas ("Pizza; refri"), aspas dobradas ("" = ") e quebras de linha dentro das aspas.
export function lerCsv(texto, separador) {
  const linhas = []
  let linha = []
  let campo = ''
  let entreAspas = false
  for (let i = 0; i < texto.length; i++) {
    const c = texto[i]
    if (entreAspas) {
      if (c === '"' && texto[i + 1] === '"') {
        campo += '"'
        i++
      } else if (c === '"') {
        entreAspas = false
      } else {
        campo += c
      }
    } else if (c === '"') {
      entreAspas = true
    } else if (c === separador) {
      linha.push(campo)
      campo = ''
    } else if (c === '\n' || c === '\r') {
      if (c === '\r' && texto[i + 1] === '\n') i++
      linha.push(campo)
      linhas.push(linha)
      linha = []
      campo = ''
    } else {
      campo += c
    }
  }
  if (campo !== '' || linha.length > 0) {
    linha.push(campo)
    linhas.push(linha)
  }
  return linhas
}

// "R$ -1.234,56" / "-1234.56" / "(50,00)" / "1.234,56-" -> número. Inválido -> NaN.
export function lerNumero(texto) {
  let t = String(texto ?? '').replace(/R\$|\s| /g, '')
  if (t === '') return NaN
  let negativo = false
  if (/^\(.*\)$/.test(t)) {
    negativo = true
    t = t.slice(1, -1)
  }
  if (t.startsWith('-')) {
    negativo = !negativo
    t = t.slice(1)
  } else if (t.endsWith('-')) {
    negativo = !negativo
    t = t.slice(0, -1)
  } else if (t.startsWith('+')) {
    t = t.slice(1)
  }
  if (!/^[\d.,]+$/.test(t)) return NaN

  // Com ponto e vírgula, o último dos dois é o separador decimal ("1.234,56" ou "1,234.56").
  // Só com vírgula: vírgula decimal ("50,9"). Só com ponto: ponto decimal ("50.9").
  const ultimoPonto = t.lastIndexOf('.')
  const ultimaVirgula = t.lastIndexOf(',')
  if (ultimaVirgula > ultimoPonto) t = t.replace(/\./g, '').replace(',', '.')
  else t = t.replace(/,/g, '')
  if ((t.match(/\./g) ?? []).length > 1) return NaN

  const numero = Number(t)
  return negativo ? -numero : numero
}

// "05/11/2026", "5/11/26", "05-11-2026", "2026-11-05" (com ou sem hora) -> "2026-11-05". Inválida -> null.
export function lerData(texto) {
  const t = String(texto ?? '').trim()
  let dia, mes, ano
  let m = t.match(/^(\d{4})-(\d{1,2})-(\d{1,2})/)
  if (m) [, ano, mes, dia] = m
  else {
    m = t.match(/^(\d{1,2})[/.-](\d{1,2})[/.-](\d{2}|\d{4})(?!\d)/)
    if (!m) return null
    ;[, dia, mes, ano] = m
    if (ano.length === 2) ano = `20${ano}`
  }
  const data = new Date(Date.UTC(Number(ano), Number(mes) - 1, Number(dia)))
  // Confere se a data existe (31/02 vira 03/03 no Date; aqui é recusada).
  if (data.getUTCFullYear() !== Number(ano) || data.getUTCMonth() !== Number(mes) - 1 || data.getUTCDate() !== Number(dia))
    return null
  return data.toISOString().slice(0, 10)
}

// Adivinha as colunas pelos nomes do cabeçalho. -1 = não achou (a pessoa escolhe na tela).
export function adivinharColunas(cabecalho) {
  const nomes = cabecalho.map((nome) => normalizar(nome.trim()))
  // Cada coluna só pode ser usada uma vez ("Data Lançamento" é a data, não a descrição).
  const usadas = []
  const achar = (...pedacos) => {
    const i = nomes.findIndex((nome, j) => !usadas.includes(j) && pedacos.some((p) => nome.includes(p)))
    if (i >= 0) usadas.push(i)
    return i
  }
  const data = achar('data', 'date')
  const valor = achar('valor', 'amount', 'quantia', 'value', 'montante')
  const descricao = achar('descri', 'histor', 'lancamento', 'title', 'titulo', 'estabelecimento', 'memo', 'detalhe')
  // Alguns bancos não têm "Valor": têm uma coluna de crédito (entradas) e outra de débito (saídas).
  const credito = valor < 0 ? achar('credito', 'entrada') : -1
  const debito = valor < 0 ? achar('debito', 'saida') : -1
  return { data, descricao, valor, credito, debito }
}

// Texto do arquivo -> separador, cabeçalho, linhas de dados e colunas adivinhadas.
export function interpretarExtrato(texto) {
  const separador = detectarSeparador(texto)
  const todas = lerCsv(texto, separador)
  const cabecalho = todas[0] ?? []
  // Cada linha guarda o número dela no arquivo (o cabeçalho é a linha 1).
  const linhas = todas.slice(1).map((campos, i) => ({ numero: i + 2, campos }))
  return { separador, cabecalho, linhas, colunas: adivinharColunas(cabecalho) }
}

// Linhas + colunas escolhidas -> transações. Valor negativo = despesa; positivo = receita.
// Sem coluna de valor (colunas.valor = -1), usa as colunas de crédito e débito.
// inverterSinal: para faturas de cartão, em que as compras vêm positivas.
// Linhas em branco são ignoradas; linhas com problema vêm com "erro" (e não são importadas).
export function montarTransacoes(linhas, colunas, inverterSinal = false) {
  return linhas
    .filter(({ campos }) => campos.some((c) => c.trim() !== ''))
    .map(({ numero, campos }) => {
      const data = lerData(campos[colunas.data])
      const descricao = (campos[colunas.descricao] ?? '').trim().replace(/\s+/g, ' ')
      let valor
      if (colunas.valor >= 0) {
        valor = lerNumero(campos[colunas.valor])
      } else {
        // Crédito e débito em colunas separadas: valor = crédito − débito (vazio conta como 0).
        const credito = lerNumero(campos[colunas.credito])
        const debito = lerNumero(campos[colunas.debito])
        valor =
          Number.isNaN(credito) && Number.isNaN(debito)
            ? NaN
            : (Number.isNaN(credito) ? 0 : Math.abs(credito)) - (Number.isNaN(debito) ? 0 : Math.abs(debito))
      }
      if (inverterSinal) valor = -valor
      let erro = null
      if (!data) erro = 'data inválida'
      else if (!descricao) erro = 'sem descrição'
      else if (Number.isNaN(valor)) erro = 'valor inválido'
      else if (Math.round(Math.abs(valor) * 100) === 0) erro = 'valor zero'
      return {
        linha: numero,
        data,
        descricao,
        valor: Number.isNaN(valor) ? 0 : Math.round(Math.abs(valor) * 100) / 100,
        tipo: valor < 0 ? 'Despesa' : 'Receita',
        erro,
      }
    })
}

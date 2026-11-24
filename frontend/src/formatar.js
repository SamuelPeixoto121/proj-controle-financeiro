// 3249.45 -> "R$ 3.249,45"
export const formatarReais = (valor) =>
  valor.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })

// "2026-11-05T00:00:00" -> "05/11/2026"
export const formatarData = (data) => data.slice(0, 10).split('-').reverse().join('/')

// Data de hoje no formato do <input type="date">: "2026-11-18".
// O formato sueco (sv-SE) já é AAAA-MM-DD e usa o fuso horário do computador.
export const hoje = () => new Date().toLocaleDateString('sv-SE')

// Mês atual no formato do <input type="month">: "2026-11".
export const mesAtual = () => hoje().slice(0, 7)

// Para buscas: "Salário" -> "salario" (sem acentos e em minúsculas).
// O NFD separa a letra do acento ("á" vira "a" + "´"), e o replace apaga os acentos.
export const normalizar = (texto) =>
  texto.normalize('NFD').replace(/\p{Diacritic}/gu, '').toLowerCase()

export const NOMES_MESES = ['jan', 'fev', 'mar', 'abr', 'mai', 'jun', 'jul', 'ago', 'set', 'out', 'nov', 'dez']

// "2026-11" -> "nov/2026"
export const formatarMes = (mes) => `${NOMES_MESES[Number(mes.slice(5, 7)) - 1]}/${mes.slice(0, 4)}`

// Prévia das parcelas, com a mesma conta da API (a 1ª fica com os centavos que sobram):
// (100, 3) -> "3x: a 1ª de R$ 33,34 e 2x de R$ 33,33"   (1200, 10) -> "10x de R$ 120,00"
export function previaDeParcelas(valorTotal, parcelas) {
  const centavos = Math.round(Number(valorTotal) * 100)
  const n = Number(parcelas)
  if (!(centavos > 0) || !Number.isInteger(n) || n < 2 || n > 60) return ''
  const base = Math.floor(centavos / n)
  if (base < 1) return 'Valor pequeno demais para tantas parcelas.'
  const primeira = centavos - base * (n - 1)
  const reais = (c) => formatarReais(c / 100)
  return primeira === base
    ? `${n}x de ${reais(base)}`
    : `${n}x: a 1ª de ${reais(primeira)} e ${n - 1}x de ${reais(base)}`
}

// Situação de uma transação: "Paga", "Recebida", "A pagar", "A receber" ou "Atrasada"
// (atrasada = ainda pendente e a data já passou).
export function situacaoDe(transacao) {
  if (transacao.pago) return transacao.tipo === 'Receita' ? 'Recebida' : 'Paga'
  if (transacao.data.slice(0, 10) < hoje()) return 'Atrasada'
  return transacao.tipo === 'Receita' ? 'A receber' : 'A pagar'
}

// Variação em % de um ano para o outro, arredondada: (1200, 1000) -> 20, (800, 1000) -> -20.
// Sem base para comparar (anterior zero), devolve null.
export function variacao(atual, anterior) {
  if (!anterior) return null
  return Math.round(((atual - anterior) / Math.abs(anterior)) * 100)
}

// Em que dia vence a fatura de uma compra no cartão (mesma regra da API).
// Compra antes do dia do fechamento: fatura que fecha neste mês; no dia do fechamento ou depois: a seguinte.
// Se vence num dia menor que o do fechamento, vence no mês seguinte ao do fechamento.
// ({ diaFechamento: 3, diaVencimento: 10 }, "2026-12-05") -> "2027-01-10"
export function vencimentoDaFatura(cartao, dataCompra) {
  const [ano, mes, dia] = dataCompra.split('-').map(Number)
  let meses = ano * 12 + (mes - 1) // conta em meses, para virar o ano sem complicação
  if (dia >= cartao.diaFechamento) meses++
  if (cartao.diaVencimento < cartao.diaFechamento) meses++
  const dois = (n) => String(n).padStart(2, '0')
  return `${Math.floor(meses / 12)}-${dois((meses % 12) + 1)}-${dois(cartao.diaVencimento)}`
}

// Contas pendentes: junta as compras do mesmo cartão e vencimento numa linha só (a fatura).
// As outras contas ficam como estão, e a ordem (por data) é mantida.
export function agruparFaturas(transacoes) {
  const resultado = []
  const faturas = new Map()
  for (const t of transacoes) {
    if (t.cartaoId == null) {
      resultado.push(t)
      continue
    }
    const chave = `${t.cartaoId}|${t.data.slice(0, 10)}`
    const fatura = faturas.get(chave)
    if (fatura) {
      fatura.valor = Math.round((fatura.valor + t.valor) * 100) / 100
      fatura.compras++
    } else {
      const nova = { fatura: true, chave, cartaoId: t.cartaoId, cartao: t.cartao, data: t.data, valor: t.valor, compras: 1, atrasada: t.atrasada }
      faturas.set(chave, nova)
      resultado.push(nova)
    }
  }
  return resultado
}

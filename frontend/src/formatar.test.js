import { describe, expect, it } from 'vitest'
import { formatarData, formatarMes, formatarReais, hoje, normalizar, previaDeParcelas, situacaoDe, variacao, vencimentoDaFatura, agruparFaturas, progressoEconomia } from './formatar.js'

// O Intl usa espaço não separável depois do "R$"; aqui ele vira espaço normal para comparar.
const semNbsp = (texto) => texto.replace(/ /g, ' ')

describe('formatação', () => {
  it('reais, datas e meses', () => {
    expect(semNbsp(formatarReais(3249.45))).toBe('R$ 3.249,45')
    expect(formatarData('2026-11-05T00:00:00')).toBe('05/11/2026')
    expect(formatarMes('2026-11')).toBe('nov/2026')
  })
})

describe('normalizar (busca sem acento)', () => {
  it.each([
    ['Salário', 'salario'],
    ['SALÁRIO', 'salario'],
    ['Ação Pão Côco Ünico çé', 'acao pao coco unico ce'],
    ['Uber 99 - R$', 'uber 99 - r$'],
    ['', ''],
  ])('%j vira %j', (texto, esperado) => {
    expect(normalizar(texto)).toBe(esperado)
  })

  it('"agua" acha "Conta de água" e "luz" não acha "Aluguel"', () => {
    expect(normalizar('Conta de água').includes(normalizar('AGUA'))).toBe(true)
    expect(normalizar('Aluguel').includes('luz')).toBe(false)
  })
})

describe('previaDeParcelas (mesma conta da API)', () => {
  it.each([
    ['1200', '10', '10x de R$ 120,00'],
    ['100', '3', '3x: a 1ª de R$ 33,34 e 2x de R$ 33,33'],
    ['0.1', '3', '3x: a 1ª de R$ 0,04 e 2x de R$ 0,03'],
    ['0.1', '10', '10x de R$ 0,01'],
    ['1234.56', '7', '7x: a 1ª de R$ 176,40 e 6x de R$ 176,36'],
  ])('%s em %sx = %j', (valor, parcelas, esperado) => {
    expect(semNbsp(previaDeParcelas(valor, parcelas))).toBe(esperado)
  })

  it('valor pequeno demais avisa', () => {
    expect(previaDeParcelas('0.05', '10')).toBe('Valor pequeno demais para tantas parcelas.')
  })

  it.each([
    ['', '10'],
    ['100', '1'],
    ['100', '61'],
    ['100', ''],
    ['100', '2.5'],
  ])('sem prévia para valor %j e parcelas %j', (valor, parcelas) => {
    expect(previaDeParcelas(valor, parcelas)).toBe('')
  })
})

describe('situacaoDe', () => {
  const amanha = new Date(Date.now() + 86_400_000).toLocaleDateString('sv-SE')
  it.each([
    [{ pago: true, tipo: 'Despesa', data: '2020-01-01T00:00:00' }, 'Paga'],
    [{ pago: true, tipo: 'Receita', data: '2099-01-01T00:00:00' }, 'Recebida'],
    [{ pago: false, tipo: 'Despesa', data: `${amanha}T00:00:00` }, 'A pagar'],
    [{ pago: false, tipo: 'Receita', data: `${amanha}T00:00:00` }, 'A receber'],
    [{ pago: false, tipo: 'Despesa', data: `${hoje()}T00:00:00` }, 'A pagar'], // hoje ainda não é atrasada
    [{ pago: false, tipo: 'Despesa', data: '2020-01-01T00:00:00' }, 'Atrasada'],
  ])('%o é %j', (transacao, esperado) => {
    expect(situacaoDe(transacao)).toBe(esperado)
  })
})

describe('variacao (comparação com o ano anterior)', () => {
  it('calcula a % e arredonda', () => {
    expect(variacao(1200, 1000)).toBe(20)
    expect(variacao(800, 1000)).toBe(-20)
    expect(variacao(1000, 1000)).toBe(0)
    expect(variacao(100, 300)).toBe(-67) // −66,67%
    expect(variacao(0, 500)).toBe(-100)
  })

  it('sem valor no ano anterior não há % (evita dividir por zero)', () => {
    expect(variacao(500, 0)).toBeNull()
    expect(variacao(0, 0)).toBeNull()
  })

  it('base negativa: melhorar de −100 para −50 é subir', () => {
    expect(variacao(-50, -100)).toBe(50)
  })
})

describe('vencimentoDaFatura (mesma regra da API)', () => {
  const nubank = { diaFechamento: 3, diaVencimento: 10 }
  const fechaDia25 = { diaFechamento: 25, diaVencimento: 5 }
  it.each([
    [nubank, '2030-12-01', '2030-12-10'], // antes do fechamento
    [nubank, '2030-12-03', '2031-01-10'], // no dia do fechamento: próxima (e vira o ano)
    [nubank, '2030-12-31', '2031-01-10'],
    [fechaDia25, '2030-12-24', '2031-01-05'], // vence no mês seguinte ao do fechamento
    [fechaDia25, '2030-12-25', '2031-02-05'],
    [fechaDia25, '2031-01-10', '2031-02-05'],
  ])('%o, compra em %s -> vence %s', (cartao, compra, vence) => {
    expect(vencimentoDaFatura(cartao, compra)).toBe(vence)
  })
})

describe('agruparFaturas (contas do mês)', () => {
  it('junta as compras do mesmo cartão e vencimento e mantém o resto', () => {
    const lista = [
      { id: 1, cartaoId: 7, cartao: 'Nubank', data: '2030-12-10T00:00:00', valor: 50.1, atrasada: false },
      { id: 2, cartaoId: null, data: '2030-12-10T00:00:00', valor: 999 },
      { id: 3, cartaoId: 7, cartao: 'Nubank', data: '2030-12-10T00:00:00', valor: 0.2, atrasada: false },
      { id: 4, cartaoId: 8, cartao: 'Inter', data: '2030-12-10T00:00:00', valor: 30, atrasada: false },
      { id: 5, cartaoId: 7, cartao: 'Nubank', data: '2031-01-10T00:00:00', valor: 10, atrasada: false },
    ]
    const r = agruparFaturas(lista)
    expect(r.map((x) => x.fatura ? `${x.cartao} ${x.data.slice(0, 10)}` : x.id)).toEqual([
      'Nubank 2030-12-10', 2, 'Inter 2030-12-10', 'Nubank 2031-01-10',
    ])
    expect(r[0].valor).toBe(50.3) // sem 50.300000000000004
    expect(r[0].compras).toBe(2)
  })
})

describe('progressoEconomia (meta de economia)', () => {
  it('meta batida: barra cheia e quanto sobrou', () => {
    expect(progressoEconomia(3930.62, 1000)).toEqual({ situacao: 'batida', porcentagem: 393, barra: 100, valor: 2930.62 })
    expect(progressoEconomia(1000, 1000)).toEqual({ situacao: 'batida', porcentagem: 100, barra: 100, valor: 0 })
  })

  it('faltando: quanto falta, sem arredondar para 100%', () => {
    expect(progressoEconomia(750, 1000)).toEqual({ situacao: 'faltando', porcentagem: 75, barra: 75, valor: 250 })
    expect(progressoEconomia(999.99, 1000)).toEqual({ situacao: 'faltando', porcentagem: 99, barra: 99, valor: 0.01 })
    expect(progressoEconomia(0, 1000)).toEqual({ situacao: 'faltando', porcentagem: 0, barra: 0, valor: 1000 })
  })

  it('mês negativo: barra vazia e o valor negativo', () => {
    expect(progressoEconomia(-300.1, 1000)).toEqual({ situacao: 'negativo', porcentagem: 0, barra: 0, valor: -300.1 })
  })
})

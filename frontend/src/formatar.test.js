import { describe, expect, it } from 'vitest'
import { formatarData, formatarMes, formatarReais, hoje, normalizar, previaDeParcelas, situacaoDe, variacao } from './formatar.js'

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

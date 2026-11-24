import { describe, expect, it } from 'vitest'
import { gerarCsv } from './csv.js'

const transacao = (descricao, valor, extra = {}) => ({
  descricao,
  valor,
  data: '2026-11-05T00:00:00',
  tipo: 'Despesa',
  categoria: { nome: 'Mercado' },
  pago: true,
  ...extra,
})

describe('gerarCsv (formato do Excel em português)', () => {
  const csv = gerarCsv([
    transacao('Salário', 3500.5, { tipo: 'Receita', categoria: { nome: 'Salário' } }),
    transacao('Aluguel', 1200, { pago: false, data: '2020-01-10T00:00:00' }),
    transacao('Pizza; refri "grande"', 45.9),
    transacao('Linha 1\nLinha 2', 0.1),
    transacao('=1+1', 10),
    transacao('Café', 1234567.89),
  ])
  const linhas = csv.slice(1).split('\r\n')

  it('começa com o BOM e o cabeçalho', () => {
    expect(csv.charCodeAt(0)).toBe(0xfeff)
    expect(linhas[0]).toBe('Data;Descrição;Categoria;Tipo;Valor;Situação')
  })

  it('valores com vírgula, 2 casas e sem ponto de milhar; situação', () => {
    expect(linhas[1]).toBe('05/11/2026;Salário;Salário;Receita;3500,50;Recebida')
    expect(linhas[2]).toBe('10/01/2020;Aluguel;Mercado;Despesa;1200,00;Atrasada')
    expect(csv).toContain(';Café;Mercado;Despesa;1234567,89;Paga')
  })

  it('; aspas e quebra de linha vão entre aspas', () => {
    expect(linhas[3]).toBe('05/11/2026;"Pizza; refri ""grande""";Mercado;Despesa;45,90;Paga')
    expect(csv).toContain('"Linha 1\nLinha 2";Mercado;Despesa;0,10;Paga')
  })

  it('texto que começa com = ganha um apóstrofo (o Excel não executa como fórmula)', () => {
    expect(csv).toContain(";'=1+1;")
  })

  it('termina com quebra de linha, e lista vazia gera só o cabeçalho', () => {
    expect(csv.endsWith('\r\n')).toBe(true)
    expect(gerarCsv([])).toBe('\uFEFFData;Descrição;Categoria;Tipo;Valor;Situação\r\n')
  })
})

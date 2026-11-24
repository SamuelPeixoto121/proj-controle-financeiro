import { describe, expect, it } from 'vitest'
import { despesasPorCategoria, mesPorExtenso, porcentagemDe, totaisDe, transacoesDoAno } from './relatorio.js'

const t = (id, data, tipo, valor, categoria) => ({ id, data: `${data}T00:00:00`, tipo, valor, categoria: categoria && { nome: categoria } })

describe('mesPorExtenso', () => {
  it('escreve o mês por extenso', () => {
    expect(mesPorExtenso('2026-11')).toBe('novembro de 2026')
    expect(mesPorExtenso('2027-03')).toBe('março de 2027')
  })
})

describe('porcentagemDe', () => {
  it('uma casa decimal e total zero vira 0%', () => {
    expect(porcentagemDe(1, 3)).toBe(33.3)
    expect(porcentagemDe(50, 200)).toBe(25)
    expect(porcentagemDe(10, 0)).toBe(0)
  })
})

describe('transacoesDoAno', () => {
  it('filtra o ano e ordena da mais antiga para a mais nova', () => {
    const lista = [t(3, '2026-05-01'), t(1, '2025-12-31'), t(2, '2026-01-01'), t(4, '2026-05-01'), t(5, '2027-01-01')]
    expect(transacoesDoAno(lista, 2026).map((x) => x.id)).toEqual([2, 3, 4])
  })
})

describe('despesasPorCategoria', () => {
  it('soma só as despesas, da maior para a menor, com a %', () => {
    const lista = [
      t(1, '2026-01-01', 'Despesa', 100.1, 'Mercado'),
      t(2, '2026-01-02', 'Despesa', 300, 'Moradia'),
      t(3, '2026-01-03', 'Despesa', 0.2, 'Mercado'),
      t(4, '2026-01-04', 'Receita', 5000, 'Salário'),
      t(5, '2026-01-05', 'Despesa', 99.7, null),
    ]
    expect(despesasPorCategoria(lista)).toEqual([
      { categoria: 'Moradia', valor: 300, porcentagem: 60 },
      { categoria: 'Mercado', valor: 100.3, porcentagem: 20.1 },
      { categoria: 'Sem categoria', valor: 99.7, porcentagem: 19.9 },
    ])
  })

  it('sem despesas: lista vazia', () => {
    expect(despesasPorCategoria([t(1, '2026-01-01', 'Receita', 10, 'X')])).toEqual([])
  })
})

describe('totaisDe', () => {
  it('soma receitas e despesas sem erro de centavos', () => {
    const lista = [t(1, '2026-01-01', 'Receita', 0.1), t(2, '2026-01-01', 'Receita', 0.2), t(3, '2026-01-01', 'Despesa', 5)]
    expect(totaisDe(lista)).toEqual({ receitas: 0.3, despesas: 5 })
  })
})

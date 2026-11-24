import { describe, expect, it } from 'vitest'
import {
  adivinharColunas,
  decodificar,
  detectarSeparador,
  interpretarExtrato,
  lerCsv,
  lerData,
  lerNumero,
  montarTransacoes,
} from './lerExtrato.js'

describe('lerNumero', () => {
  it.each([
    ['-50.00', -50],
    ['1.234,56', 1234.56],
    ['-1.234,56', -1234.56],
    ['R$ -50,00', -50],
    ['R$ 1.234,56', 1234.56],
    ['1,234.56', 1234.56],
    ['50,9', 50.9],
    ['(50,00)', -50],
    ['1.234,56-', -1234.56],
    ['+10', 10],
    ['0,01', 0.01],
    [' 12 ', 12],
    ['1 234,00', 1234],
  ])('%j vira %d', (texto, esperado) => {
    expect(lerNumero(texto)).toBe(esperado)
  })

  it.each(['abc', '', '1.2.3', '12a'])('%j é inválido (NaN)', (texto) => {
    expect(lerNumero(texto)).toBeNaN()
  })
})

describe('lerData', () => {
  it.each([
    ['05/11/2026', '2026-11-05'],
    ['5/1/2026', '2026-01-05'],
    ['05/11/26', '2026-11-05'],
    ['05-11-2026', '2026-11-05'],
    ['05.11.2026', '2026-11-05'],
    ['2026-11-05', '2026-11-05'],
    ['2026-11-05T10:30:00', '2026-11-05'],
    ['05/11/2026 10:30', '2026-11-05'],
    ['29/02/2028', '2028-02-29'],
  ])('%j vira %j', (texto, esperado) => {
    expect(lerData(texto)).toBe(esperado)
  })

  it.each(['29/02/2027', '31/04/2026', '13/13/2026', 'ontem', ''])('%j é inválida (null)', (texto) => {
    expect(lerData(texto)).toBeNull()
  })
})

describe('lerCsv e detectarSeparador', () => {
  it('entende aspas, ; dentro de aspas e aspas dobradas', () => {
    expect(lerCsv('a;"b; c";"d ""e"""\n1;2;3', ';')).toEqual([
      ['a', 'b; c', 'd "e"'],
      ['1', '2', '3'],
    ])
  })

  it('entende quebra de linha dentro de aspas e \\r\\n', () => {
    expect(lerCsv('"linha 1\nlinha 2",5\r\nx,6\r\n', ',')).toEqual([
      ['linha 1\nlinha 2', '5'],
      ['x', '6'],
    ])
  })

  it.each([
    ['Data;Descrição;Valor\n', ';'],
    ['Data,Valor,Identificador,Descrição\n', ','],
    ['date,title,"amount, BRL"\n', ','],
    ['Data\tHistórico\tValor\n', '\t'],
  ])('separador de %j é %j', (texto, esperado) => {
    expect(detectarSeparador(texto)).toBe(esperado)
  })
})

describe('decodificar', () => {
  it('UTF-8 com BOM: tira o BOM e mantém os acentos', () => {
    const bytes = new TextEncoder().encode('\uFEFFData;Descrição;Valor')
    expect(decodificar(bytes)).toBe('Data;Descrição;Valor')
  })

  it('Windows-1252 (muitos bancos): "Descrição" sai certo', () => {
    const bytes = Uint8Array.from([...'Data;Descrição;Valor'].map((c) => c.charCodeAt(0)))
    expect(decodificar(bytes)).toBe('Data;Descrição;Valor')
  })
})

describe('extratos de exemplo', () => {
  it('conta com "," e ponto decimal', () => {
    const e = interpretarExtrato(
      'Data,Valor,Identificador,Descrição\n' +
        '05/11/2026,5200.00,abc-1,Transferência recebida - EMPRESA X\n' +
        '08/11/2026,-680.90,abc-2,"Compra no débito - SUPERMERCADO, LOJA 2"\n' +
        '\n' +
        '10/11/2026,-1200.00,abc-3,Pagamento de boleto - ALUGUEL\n',
    )
    expect(e.colunas).toEqual({ data: 0, descricao: 3, valor: 1, credito: -1, debito: -1 })
    const t = montarTransacoes(e.linhas, e.colunas)
    expect(t).toHaveLength(3) // a linha vazia é ignorada
    expect(t[0]).toEqual({
      linha: 2,
      data: '2026-11-05',
      descricao: 'Transferência recebida - EMPRESA X',
      valor: 5200,
      tipo: 'Receita',
      erro: null,
    })
    expect(t[1]).toMatchObject({ tipo: 'Despesa', valor: 680.9, descricao: 'Compra no débito - SUPERMERCADO, LOJA 2' })
    expect(t[2].linha).toBe(5) // o número da linha no arquivo pula a vazia
  })

  it('conta com ";", vírgula decimal, "Data Lançamento" e saldo anterior', () => {
    const e = interpretarExtrato(
      'Data Lançamento;Histórico;Valor (R$);Saldo (R$)\r\n' +
        '01/11/2026;SALDO ANTERIOR;;1.000,00\r\n' +
        '03/11/2026;PIX ENVIADO   MARIA;-1.234,56;-234,56\r\n' +
        '04/11/2026;SALARIO;5.000,00;4.765,44\r\n',
    )
    expect(e.colunas).toMatchObject({ data: 0, descricao: 1, valor: 2 })
    const t = montarTransacoes(e.linhas, e.colunas)
    expect(t[0].erro).toBe('valor inválido')
    expect(t[1]).toMatchObject({ tipo: 'Despesa', valor: 1234.56, descricao: 'PIX ENVIADO MARIA', erro: null })
    expect(t[2]).toMatchObject({ tipo: 'Receita', valor: 5000, erro: null })
  })

  it('fatura de cartão: com "inverter", compra positiva vira despesa', () => {
    const e = interpretarExtrato(
      'date,title,amount\n2026-11-02,Restaurante,89.90\n2026-11-05,Pagamento recebido,-500.00\n',
    )
    expect(e.colunas).toMatchObject({ data: 0, descricao: 1, valor: 2 })
    const invertida = montarTransacoes(e.linhas, e.colunas, true)
    expect(invertida[0]).toMatchObject({ tipo: 'Despesa', valor: 89.9 })
    expect(invertida[1]).toMatchObject({ tipo: 'Receita', valor: 500 })
    expect(montarTransacoes(e.linhas, e.colunas, false)[0].tipo).toBe('Receita')
  })

  it('crédito e débito em colunas separadas', () => {
    const e = interpretarExtrato(
      'Data;Histórico;Docto.;Crédito (R$);Débito (R$);Saldo (R$)\n' +
        '02/11/2026;SALDO ANTERIOR;;;;100,00\n' +
        '03/11/2026;TED RECEBIDA;123;2.500,00;;2.600,00\n' +
        '04/11/2026;CONTA DE LUZ;456;;189,90;2.410,10\n' +
        '05/11/2026;TARIFA;;;-12,00;2.398,10\n',
    )
    expect(e.colunas).toMatchObject({ valor: -1, credito: 3, debito: 4, descricao: 1 })
    const t = montarTransacoes(e.linhas, e.colunas)
    expect(t[0].erro).toBe('valor inválido')
    expect(t[1]).toMatchObject({ tipo: 'Receita', valor: 2500, erro: null })
    expect(t[2]).toMatchObject({ tipo: 'Despesa', valor: 189.9 })
    expect(t[3]).toMatchObject({ tipo: 'Despesa', valor: 12 })
  })

  it('marca o problema de cada linha', () => {
    const e = interpretarExtrato('Data;Descrição;Valor\n31/02/2026;X;10\n05/11/2026;;10\n05/11/2026;Y;abc\n05/11/2026;Z;0,00\n')
    expect(montarTransacoes(e.linhas, e.colunas).map((l) => l.erro)).toEqual([
      'data inválida',
      'sem descrição',
      'valor inválido',
      'valor zero',
    ])
  })

  it('cabeçalho desconhecido e arquivo vazio não quebram', () => {
    expect(adivinharColunas(['a', 'b', 'c'])).toEqual({ data: -1, descricao: -1, valor: -1, credito: -1, debito: -1 })
    expect(interpretarExtrato('').linhas).toHaveLength(0)
  })
})

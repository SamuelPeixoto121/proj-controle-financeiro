import { formatarData, situacaoDe } from './formatar.js'

// Um campo do CSV. Vai entre aspas se tiver ; aspas ou quebra de linha (aspas internas viram "").
// Texto que começa com = + - @ ganha um ' na frente, para o Excel não tratar como fórmula.
function campo(valor) {
  let texto = String(valor ?? '')
  if (/^[=+\-@]/.test(texto)) texto = `'${texto}`
  return /[;"\r\n]/.test(texto) ? `"${texto.replaceAll('"', '""')}"` : texto
}

// Monta o CSV no formato do Excel em português: separador ";" e vírgula decimal.
// O caractere invisível no início (BOM) avisa ao Excel que o arquivo é UTF-8, para os acentos aparecerem certos.
export function gerarCsv(transacoes) {
  const linhas = [
    ['Data', 'Descrição', 'Categoria', 'Tipo', 'Valor', 'Situação'],
    ...transacoes.map((t) => [
      formatarData(t.data),
      t.descricao,
      t.categoria?.nome,
      t.tipo,
      t.valor.toFixed(2).replace('.', ','),
      situacaoDe(t),
    ]),
  ]
  return String.fromCharCode(0xfeff) + linhas.map((linha) => linha.map(campo).join(';')).join('\r\n') + '\r\n'
}

// Faz o navegador baixar um arquivo com esse texto.
export function baixarArquivo(nome, texto) {
  const url = URL.createObjectURL(new Blob([texto], { type: 'text/csv;charset=utf-8' }))
  const link = document.createElement('a')
  link.href = url
  link.download = nome
  link.click()
  URL.revokeObjectURL(url)
}

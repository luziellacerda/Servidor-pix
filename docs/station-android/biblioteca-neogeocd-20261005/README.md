# Catálogo Station 14 — 05/10/2026

Exportação sanitizada do índice efetivo, cruzada com o catálogo assinado HTTPS. Arquivos TSV usam tabulação, descrições multilinha e campos vazios intencionais. Ler com `csv.DictReader(..., delimiter='\t')`, não contando linhas do arquivo.

| Arquivo | Conteúdo |
|---|---|
| [catalogo-completo.tsv](catalogo-completo.tsv) | 2.212 jogos visíveis: nomes, plataforma, IDs, capa, sinopse, metadados, descritor/hash do download |
| [neogeocd-jogos-capas-downloads.tsv](neogeocd-jogos-capas-downloads.tsv) | 50 discos CD, capas exatas e sinopses, formato RAW/CHD |
| [compatibilidade-ids.tsv](compatibilidade-ids.tsv) | 255 IDs ocultos mantidos para recibos anteriores |
| [metadados-pendentes.tsv](metadados-pendentes.tsv) | 43 jogos anteriores sem sinopse na fonte; nenhum CD nessa pendência |
| [resumo-catalogo.json](resumo-catalogo.json) | Contagens e reconciliação |
| [evidencia-http.json](evidencia-http.json) | Catálogo/grants assinados, oito pares capa/download, 50 capas HTTPS com quatro workers |
| [evidencia-estado-final.json](evidencia-estado-final.json) | API/scanner/timer/índice, serviços preservados e zero registros sintéticos |

As 2.417 linhas internas anteriores são exatamente iguais no índice novo; seus fatos de capa/ROM reutilizam a auditoria publicada do catálogo9. Os 50 CD foram cruzados com recibos de verificação CHD integral e revistas originais. SHA da imagem de origem e SHA da imagem JPEG compilada são campos diferentes.

Sem caminhos absolutos de jogos, URLs de ROM, Bearer, chaves, dados pessoais ou BIOS nos exports. Para baixar, o aplicativo usa o `itemId` no contrato autenticado, não os arquivos TSV ou o nome do jogo. BIOS CD ausente: catálogo/downloads disponíveis; execução Android exige firmware e a ponte nova.

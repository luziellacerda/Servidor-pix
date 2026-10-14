# Wind Waker: verificação local da segunda edição do catálogo

Em 14/10/2026, conferi **somente uma cópia local** da segunda entrada de *The
Legend of Zelda: The Wind Waker*. Ela corresponde ao item
`station_d0a6aa65e5900ba9167a200f8dccfae5`, apresentado no snapshot de
catálogo como `The Legend of Zelda The Wind Waker`. A origem lida foi
`D:\TurboRoms\roms\gamecube\PT-BR Traduzidos\The Legend of Zelda - The Wind Waker.iso`.
Este caminho identifica a cópia examinada no PC; não identifica o arquivo
atualmente servido no Linux. Nenhum binário de jogo, credencial ou dado de
aparelho integra esta evidência.

| Verificação local | Resultado |
| --- | --- |
| Tamanho da cópia | 1.459.978.240 bytes |
| SHA-256 calculado | `3ea3cc8f62acfaafe2d2adb96090bf87cfabd01f89e0631d571517faf670d5f1` |
| Comparação com o descritor documental do item | Tamanho e SHA-256 coincidem |
| `DolphinTool.exe header -j` | `GZLE01`, `USA`, `NTSC-U`, revisão 0 |
| `DolphinTool.exe verify` | Código de saída 0; `Problems Found: No`; CRC32 `fdef2657`; SHA1 `a8948d98eba82c4c54cccb79eabc8aec08400863` |
| Tabela FST da imagem | Deslocamento 3.948.288; tamanho 57.054; entrada raiz informa 2.391 entradas no total, incluindo a raiz (2.390 entradas seguintes) |
| Intervalo que falhou na outra edição | Os 32 bytes em `0x55e3c040–0x55e3c060` existem nesta cópia |

O verificador também escreveu `Error creating key` antes dos hashes. A execução
terminou com código 0 e `Problems Found: No`; conservamos a mensagem no
registro, sem tratá-la como prova de jogabilidade. O resultado comprova apenas
que a cópia local passou a checagem estática efetuada com essa ferramenta.

## Comparação e correção mínima

A entrada que falhou no A56, `station_12b835c9539b4800a6bb29ca00a25f94`,
é anunciada como edição **Europe**, mas a imagem baixada tem 546.776.387
bytes e cabeçalho `GZLE01` (**USA**). O Dolphin tentou ler no byte
1.440.989.248, além do fim desse arquivo. A entrada distinta
`station_d0a6aa65e5900ba9167a200f8dccfae5` não promete região Europa e
seu arquivo local de 1.459.978.240 bytes contém esse intervalo.

No servidor, obter primeiro o índice produtivo e o artefato atualmente
configurados, conferir seus hashes e testar a resposta da API. Se coincidirem
com este item verificado, **oferecer a segunda entrada para novo download e
teste offline no A56**. Suspender somente a entrada Europe defeituosa conforme
[o diagnóstico R149](ERRO-LEITURA-WIND-WAKER-20261014.md): ocultar, retirar seu
`artifact` do índice vigente, avançar a revisão atomicamente e impedir que a
varredura a republique. Não substituir os bytes ou o vínculo da entrada Europe
pela edição USA, nem alterar a lógica do APK ou outros jogos.

A imagem servida no Linux e a revisão atual do catálogo **ainda não foram
conferidas diretamente** por esta análise. Em 14/10/2026, após baixar a
segunda entrada no A56, o usuário respondeu que ela **entrou no jogo** ao
abrir para jogar sozinho. Esse relato confirma a abertura observada pelo
usuário; não constitui captura de log, comparação do SHA do artefato servido
ou teste prolongado de jogabilidade. A entrada anteriormente instalada no
telefone continua associada ao seu próprio ID; publicar revisão não a troca
automaticamente.

Os campos conferidos em formato legível por máquina estão em
[`evidence/VALIDACAO-WIND-WAKER-ALTERNATIVA-20261014.json`](evidence/VALIDACAO-WIND-WAKER-ALTERNATIVA-20261014.json).

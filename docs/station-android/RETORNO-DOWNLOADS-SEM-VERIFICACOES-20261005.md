# Retorno: downloads e preparação rápidos — 05/10/2026

## Estado para incorporar ao aplicativo

O mantenedor informou que o Neo Geo CD estava lento em **Baixando e Preparando** e pediu remover conferências de integridade/conteúdo desnecessárias e diminuir a espera para começar.

O servidor foi medido com downloads completos. A correção está implementada e compilada no cliente Android, com testes; **ainda precisa entrar em um APK assinado e atualizado no aparelho**. A última base recebida é R20, retorno `ceb5efab213d8364ffdeacfd49d0baa55c39df2a`, APK SHA256 `64eae3ab4dd253e25ee826dd23bf02c947e5cbd1db70e6b07f759d988d465904`. Há ajuste visual posterior solicitado para deixar consoles só na sinopse; preservar uma sucessora comprovada ao incorporar estes módulos.

Fonte preparada: TurboElden, commit `6f012a7`, branch `feat/station-raw-transfer-20261005`, pasta `versions/station-raw-transfer-20261005`. Seu README e receitas descrevem compilação e empacotamento. A fonte R16 do retorno continua preservada.

## Por que o Neo Geo CD demora

O SHA dos jogos já estava desativado em R16, mas `StationInstaller` ainda copiava o RAW completo depois de baixar. Metal Slug recebe **431,226 MB** e gravava aproximadamente **862,451 MB** no total, além dos pequenos manifestos. Seu progresso terminava a rede em 50% por contar essa segunda cópia.

A rota externa também está mais lenta que a leitura/entrega internas. Os seis testes novos usaram grants próprios de uso único e consumiram cada corpo inteiro:

| Plataforma e jogo | Bytes | API local | Nginx local | HTTPS público |
| --- | ---: | ---: | ---: | ---: |
| Neo Geo — The King of Fighters 2003 bootleg, ZIP | 87.479.934 | 303,748 MB/s; 0,2880 s | 282,242 MB/s; 0,3099 s | 4,000 MB/s; 21,8703 s |
| Neo Geo CD — Metal Slug, CHD/RAW | 431.225.741 | 311,329 MB/s; 1,3851 s | 311,232 MB/s; 1,3855 s | 3,225 MB/s; 133,7035 s |

Autorização CD local 8,23–16,37 ms; cabeçalhos internos 6,49–7,18 ms. No HTTPS CD, janelas de dez segundos variaram entre 0,913 e 4,426 MB/s. A espera e variação observadas ocorrem no caminho externo. Esses resultados não isolam operadora, Cloudflare, destino ou Wi-Fi do aparelho e **não são uma medição do telefone**.

Não há hash completo do jogo em cada autorização/GET. A API confere metadados do arquivo e transmite a cópia preparada em NVMe; não recompila, converte ou recompacta durante HTTP. Os 50 CHDs são entregues como RAW `.chd`, somando 15.170.394.032 bytes. O link físico segue 100Mbps FullDuplex; sem erros/drops nas amostras. Não existe cap de MB/s na rota Station.

## O que foi retirado da fila de downloads

- Segunda cópia RAW: o CHD completo recebido muda de diretório por renomeação atômica e torna-se o arquivo instalado.
- Conferências redundantes depois da transferência: a fila usa o recibo dos bytes já recebidos, sem reabrir o corpo para verificar hash, cabeçalho, tamanho ou referências CUE/M3U.
- CRC do corpo durante extração ZIP: a ponte usa `zip:ignorecrc32=1` da libarchive. Os 1.363 ZIP do catálogo atual passam pela política nova; os outros 849 RAW não precisam de decodificador.
- Consulta antecipada do catálogo completo só porque a sessão mudou: o cliente pede a autorização do item salvo e só atualiza o catálogo quando o grant assinado acusa revisão diferente, com uma tentativa de conciliação.
- Contagem e reserva em dobro dos RAW: rede chega a 100%; Preparando publica os pequenos manifestos. Se um filesystem não permitir renomeação atômica, há cópia transacional com espaço suficiente e percentual sem regressão.

O fluxo ainda precisa receber os bytes, escrever o arquivo, obter a licença/grant e extrair ZIP para disponibilizar seus arquivos. Esses passos seguem sem sleeps ou pacing de bytes/s. Cancelamento, publicação do recibo, caminhos e saves são transacionais; não voltaram hashes de jogos ou varreduras de legado.

## Artefatos prontos e prova

- Cliente Android API36/Java8/D8 min26: **156.144 bytes**, SHA256 `0f2748eaf8f4f1622f2537c94495d48843f2d8a69ae933485de2fa35772b9ddd`.
- Ponte de extração ARM64/API26: **1.759.680 bytes**, SHA256 `3edbb4bd08af6ae4585b53c3243c56921f2bedac475298612bf88f042e62c54c`. Segmentos LOAD alinhados16KiB; compilação/link sem erros.
- **821 verificações Java / 18 suítes** e **14 JNI reais** passaram. JNI executado em Linux com libarchive3.7.2; Android compilado com as dependências originais3.8.9/xz5.8.3. Não é execução Android da revisão nova.
- Pacote privado pronto para transportar: `/mnt/DADOS/station-raw-transfer-check-r2-20261005/station-download-sem-verificacoes-20261005.zip`, modo0600, SHA256 `00fef3d9eab8a450ffb05dfcfe293d41fea87358425b243c62f70f0e9750ce5e` (718.135 bytes). Inclui módulos, fontes, receitas e recibos; sem APK, jogo, BIOS ou chave privada.
- Recibos sanitizados em [evidencia-downloads-20261005](evidencia-downloads-20261005). O hash dos corpos foi calculado apenas no instrumento do teste do servidor, sem ligar essa conferência no Android.

Substituir somente `classes28.dex` e `lib/arm64-v8a/libstation_archive.so` sobre a base vigente conciliada. Preservar carousel/recursos/N64/MAME/classes35, assinatura original, licença, jogos e saves. A receita disponível exige o SHA exato R20; uma sucessora visual precisa de seu recibo antes de atualizar esse guard. Alinhar16KiB, assinar com o certificado original e atualizar sem desinstalar ou limpar dados. A ponte CD preparada anteriormente continua separada e não deve trazer o APK R18 de volta.

## Produção depois dos testes

API Station PID347227, DLL `0b3f5da385216d216fb55220789f55c40b8eb304b7b1a4759cc154b1aa3f3ab0`, fonte931030b. Catálogo14 com **2.212 visíveis / 255 compatíveis / 50 CD**, índice `07ad4c3fda41a19c23745436c4c45c97a70b2c7688eff788612fe22743823a92`. Timer/scanner e configuração de conteúdo preservados; nenhum deploy ou restart de serviço. Licença sintética e suas linhas de teste removidas; PIDs de Station, Nginx, túnel, Suite e PIX idênticos antes/depois.

Ainda falta o recibo do APK novo no telefone e a medição das fases com CHD grande. A diferença de velocidade do caminho público permanece um resultado separado; não declarar velocidade interna de311MB/s como taxa de internet nem aceleração já instalada no aparelho.

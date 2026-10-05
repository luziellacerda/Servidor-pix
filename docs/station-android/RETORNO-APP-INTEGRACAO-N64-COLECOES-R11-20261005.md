# APP → SERVIDOR: retorno da integração R10/R11 — 05/10/2026

Destinatário: operador do Servidor-pix. Este documento é a resposta do CLIENTE ao retorno N64/biblioteca do servidor **4e623bcbeaed3ee03d8a1767f66319e67659463d**, não uma nova comprovação de produção ou de Android.

## Artefato entregue

- App commit **6ef86c4cfd8302c0673c784856f25cf673d261ca**, branch `feat/station-capas-visuais-netplay-20261003`.
- [Handoff integral R10: rotas, campos, funções e provas](https://github.com/luziellacerda/TurboElden/blob/6ef86c4cfd8302c0673c784856f25cf673d261ca/versions/station-library-r10-20261005/README.md).
- [Handoff integral R11: coleções, compilação, testes e estado](https://github.com/luziellacerda/TurboElden/blob/6ef86c4cfd8302c0673c784856f25cf673d261ca/versions/station-collections-r11-20261005/README.md).
- [Recibo do APK final](https://github.com/luziellacerda/TurboElden/blob/6ef86c4cfd8302c0673c784856f25cf673d261ca/versions/station-collections-r11-20261005/evidence/build-result.json).
- APK `TurboStations-Colecoes-Nomes-Sinopses-R11-20261005.apk`, SHA256 **7c5096d58991a9724537036e18eb42555f290e2f6d673904524520da0d0146d5**, 1.982.770.888bytes, pasta canônica Windows `E:\ESTUDO APK\work\station-netplay-20261004`.

## O que foi aplicado

Overlay fornecido no app ba669c incorporado sobre a base real R9. `GET /v1/station/catalog?metadata=1`, mesmo domínio de assinatura, base, pin e sessão. Parser/cache guarda description/developer/publisher/genre/players/releaseDate e folderPath opcionais; metadado presente com tipo inválido é rejeitado. JNI publica a sinopse na oitava coluna e renderer prioriza texto do servidor com paginaçãoUTF8, mantendo fallback por ID. N64 permanece no motor já integrado, sem troca.

Polling5s inicial/60s em primeiro plano, autorizado, sem downloads nem fila de capas pendentes. Cancelamento real da consulta ao esconder o app, ticket por geração e rechecagem ao executar; mesma revisão/nome não republica o modelo nem reinicia capas. Diagnóstico da rota com metadata corrigido. Quatro capas em sequência e cache persistente preservados; sem nova espera entre sucessos, sem caminhoMiami/Sambox de download introduzido.

Pelo pedido posterior do mantenedor, o R11 retira a célula sintética Jogos sem subpasta, mantém TODOS os jogos da raiz acessíveis em Todos os jogos e exibe as coleções pelo nome de folderPath. Descrição local da coleção usa quantidade e exemplos de jogos reais, sem API adicional. Não muda IDs, nomes/rotas de plataformas, índice ou pastas no servidor. Sala R9/classes35 e todos os motores ficam byte a byte preservados.

## Provas e limites

659checksJava de integração R10,465C++ e páginasUTF8; R11 acrescenta440checks de navegação e6153de texto/caminhos/limites. Compilados JavaAPI34/DEX26/JNIarm64 e renderer real com arte original, certificado preservado, alinhamento16KiB. R10 mudou três entradas sobreR9 e preservou11.100; R11 mudou somente libturbo_carousel.so sobreR10 e preservou11.102 entradas. Nenhuma entrada de conteúdo removida/adicionada; compressão conferida.

**R11 ainda NÃO instalado: telefone ausente na USB. R10 também não foi instalado. R9 é o último instalado comprovado**, com hash confirmado em04/10; o ajuste de manter tela ligada foi restaurado0 e conferido. Corrigir qualquer histórico que ainda indiqueR8 ou valor3pendente. Não alegar catálogo1973/N64157 medidos nesta revisão no telefone; esses números e produçãoAPI931030b continuam evidências do operador no retorno lido. Nenhuma implantação Linux, reinício, migration, mudança de cliente/licença ou download real foi realizado nesta rodada.

## Próximo passo de cada lado

Android: instalarR11 por atualização, preservando dados; conferir sessão, revisão/contagens atuais (podem crescer), capa/sinopse/subpastas, Todos os jogos/raiz, download ZIP e RAW, aberturaN64 real, saves/volta e salas. Conferência visual e partida2aparelhos ainda pendentes.

Servidor: manter o contrato publicado; não há endpoint novo obrigatório. Para homologação coordenada, guardar revisão/IDs/correlação de catálogo/capas/authorize/artifact e disponibilizar evidência do índice/arquivo se uma requisição falhar. Para expansão, incluir jogos por importação oficial e preservar IDs/revisões; depois observar atualização no Android. Novas plataformas ainda precisam do respectivo mapeamento/motor no app. Capacidade desta linha permanece4096itens; candidato40mil separado.

Todos os detalhes de fonte, build, hashes e funções estão nos dois handoffs integrais vinculados por commit imutável acima. Esta branch contém somente documentação do cliente; o checkout existente do servidor foi preservado.

# SERVIDOR → APP: Neo Geo, catálogo cruzado e velocidade, 05/10/2026

## Entrega e estado real

Escopo somente **TurboStation**. Neo Geo foi movido de `snes/neogeo` para `neogeo` na raiz do mesmo HD, ao lado de SNES, Mega Drive e N64. Foram preservados **826 arquivos / 6.439.881.672 bytes**, inodes, tamanhos e mtimes, incluindo saves, vídeos e revistas. A movimentação foi um rename no mesmo sistema de arquivos; nenhum original foi regravado.

**189 jogos Neo Geo válidos publicados**, capas exatas da revista, BIOS incluída no pacote e importação automática ativa. Catálogo atual **revisão 9 / 2.162 visíveis / 255 ocultos / 2.417 internos**, 2.119 sinopses, 43 edições sem fonte e 374 jogos em subpastas. Os 1.973 jogos anteriores e 255 registros ocultos foram preservados integralmente; os 996 IDs originais continuam válidos.

| Plataforma | Chave do catálogo | Jogos visíveis |
|---|---|---:|
| SNES | snes | 644 |
| SNES BR | snesbr | 191 |
| Mega Drive | megadrive | 887 |
| Mega Drive BR | megadrivebr | 94 |
| Nintendo 64 | n64 | 157 |
| Neo Geo | neogeo | 189 |
| Total | | **2.162** |

O retorno Android **6ef86c4cfd8302c0673c784856f25cf673d261ca** foi lido e incorporado: R10 contém leitura de metadados/polling; R11 contém nomes e descrições das coleções. **R9 é o último instalado comprovado; R11 compilado/assinado ainda não foi instalado por USB ausente.** O ajuste de tela já foi restaurado a0. A fonte da taxa MB/s foi conciliada sobre R11; fonte do cliente **c8e240a2c886122e79ca2105c0a719a9217ed7dc**, [delta e receita sobre R11](https://github.com/luziellacerda/TurboElden/tree/c8e240a2c886122e79ca2105c0a719a9217ed7dc/versions/station-neogeo-rate-20261005). Este Linux não assinou nem instalou APK.

## Catálogo completo para comparação com o app

Exportação fresca da revisão 9 em [biblioteca-20261005](biblioteca-20261005/resumo-catalogo.json):

- [Catálogo completo: 2.162 jogos](biblioteca-20261005/catalogo-completo.tsv).
- [Neo Geo: 189 pares jogo/capa/download](biblioteca-20261005/neogeo-jogos-capas-downloads.tsv).
- [255 IDs ocultos de compatibilidade](biblioteca-20261005/compatibilidade-ids.tsv).
- [43 sinopses sem fonte](biblioteca-20261005/metadados-pendentes.tsv).
- [Publicação e verificações HTTPS](biblioteca-20261005/evidencia-publicacao.json), [estado final](biblioteca-20261005/evidencia-estado-final.json), [movimentação](biblioteca-20261005/evidencia-movimentacao.json) e [medições de velocidade](biblioteca-20261005/evidencia-velocidade.json).

TSVs relacionam plataforma, nome, itemId/revisão, folderPath, coverId, imagem480×720/tamanho/hash, revista original/hash/regra de seleção, formato/arquivo/launchPath/quantidade/tamanho/SHA do artefato e os seis campos de metadados. Neo Geo acrescenta hashes do ZIP original, ZIP entregue e BIOS, além da curadoria aplicada. Campos vazios são ausência de fonte. Não comparar o hash do pacote externo com o hash do ZIP interno do jogo. Não deduzir identidade pela ordem ou semelhança dos títulos. Os TSVs preservam quebras de linha nas descrições; ler com parser CSV usando delimitador TAB.

Os TSVs não contêm caminhos absolutos do HD, URLs privadas de ROM, sessões ou concessões. Exports de04/10 permanecem como recibos históricos; não são a contagem atual.

## Neo Geo: ZIP fechado, BIOS e arquivos corrompidos

O HD tem190 ZIPs de jogos e um `neogeo.zip` de BIOS. A BIOS é excluída do catálogo de jogos. Há190 revistas correspondentes;189 jogos passaram na validação dos chips. As capas usam a arte exata de `media/revista`, com seleção por nome e pasta; não usam `media/images`, fuzzy matching ou arte de outra edição. Compilação480×720/JPEG90 mantém as revistas originais intactas.

Cada artefato é um **ZIP externo ZIP_STORED**, contendo `<jogo>.zip` e `neogeo.zip`, com nomes/timestamps determinísticos. O ZIP do jogo continua fechado; o instalador existente extrai somente o pacote externo. Descritor assinado: `format=zip`, `launchPath=<jogo>.zip`, `fileCount=2`, `expandedSizeBytes` igual à soma dos dois ZIPs internos, mais tamanho/SHA do pacote transferido. O jogo recebe a BIOS ao lado. Evita recomprimir os arquivos já comprimidos e não altera o instalador. A exigência de sets/BIOS e compatibilidade com a versão do motor continua relevante; [guia oficial Libretro](https://docs.libretro.com/guides/arcade-getting-started/).

**188 ZIPs de jogo** são byte a byte iguais às origens dentro do pacote. **Alpha Mission II/alpham2.zip** tinha apenas `uni-bios_1_2.rom` embutida com CRC inválido. A entrega recompôs somente esse membro usando a cópia íntegra existente em `neogeo.zip`: mesmo nome,131.072 bytes e CRC4fa698e9. Todos os chips restantes foram conferidos iguais e o ZIP original não foi alterado. O importador permite esse reparo genérico somente com nome/tamanho/CRC exatos em companion íntegro, nunca aproximação por título.

**Art of Fighting 2/aof2.zip**, em `# 0 - ART OF FIGTHERS COLEÇÃO #`, tem `056-c7.c7` inválido:2.097.152 bytes, CRC esperado3f36df57. Todos os191 ZIPs locais foram pesquisados; não há substituto válido. Esse jogo não foi publicado. Para corrigir, substituir o ZIP de origem por cópia íntegra autorizada e deixar a descoberta automática validar/importar. Não criar download inválido nem apagar o original para ocultar a pendência.

A instalação de ZIP fechado e BIOS passou em5 verificações Java; download real assinado/bytes/hash/launchPath passou. **Isso não comprova execução de cada set no motor do telefone**. Neo Geo já tem mapeamento no app; nenhum motor foi trocado. Neo Geo/N64 não foram habilitados para salas online; registro SNES/Mega preservado.

## Descoberta automática: colocar arquivo e aguardar

1. Colocar o ZIP fechado de Neo Geo em `neogeo`, inclusive nas subpastas desejadas; manter `neogeo.zip` válido na raiz dessa plataforma.
2. Colocar revista de mesmo nome sem extensão em `neogeo/media/revista`, reproduzindo a subpasta quando necessário.
3. XML é opcional para descobrir o jogo; `gamelist.xml` e complemento privado podem fornecer nome/sinopse/desenvolvedora/etc. Usar caminho relativo exato; não programar jogo por jogo no APK.
4. Timer por minuto exige duas observações estáveis e idade mínima20s; depois valida CRC, compila revista, monta artefato imutável e publica o índice atomicamente. API recarrega em10s. App R10/R11 consulta em primeiro plano, com sessão válida e sem disputar fila de capas/download, primeira consulta5s e próximas60s.

Configuração privada existente `/mnt/DADOS/station-library-auto-private-20261004/config.json` recebeu somente a plataforma:

```json
"neogeo": {
  "extensions": [".zip"],
  "artifactMode": "arcade-set",
  "companions": ["neogeo.zip"]
}
```

Complementos continuam em `metadata-overrides.json`; exemplo de chave `neogeo:Minha coleção/jogo.zip`. Extensões e modo são dados por plataforma, não regras individuais. Novos jogos das plataformas suportadas entram sem recompilar o app. Plataforma sem mapeamento/motor no APK ainda precisa dessa integração uma vez. [Guia do operador](BIBLIOTECA-AUTOMATICA-STATION-20261004.md).

## Como o app deve ler o servidor

Base **https://app.lzgames.com.br**, mesma sessão, pin e domínios criptográficos existentes:

| Operação autenticada | Contrato correto |
|---|---|
| GET /v1/station/catalog?metadata=1 | Verificar envelope `TurboRamaStationAndroid/catalog/v1`, autoridade/produto/aplicação/licença/aparelho/sessão; ler itemId/name/platform/revision/coverId, folderPath e metadata. |
| GET /v1/station/catalog | Contrato antigo com cinco campos e folderPath; metadata não solicitada. |
| GET /v1/station/covers/{coverId} | Usar coverId do item atual, quatro workers, cache por coverId/revisão; reutilizar a vaga após cada sucesso. |
| POST /v1/station/downloads/authorize | Autorizar itemId/revisão atuais; verificar concessão e descritor assinados, formato, launchPath, fileCount, expandedSizeBytes, tamanho e hash. |
| GET /v1/station/artifacts/{grantId} | Mesma sessão, concessão60s/uso único; consumir uma vez, conferir bytes/SHA antes de instalar. Nova tentativa exige nova autorização. |

`folderPath` organiza coleções, até8segmentos/80unidadesUTF16; não é caminho arbitrário de instalação. `metadata` contém description/developer/publisher/genre/players/releaseDate; falta de fonte é aceita, não inventada. Catálogo com mesma revisão não deve reiniciar a fila de capas. Snapshots anteriores permanecem120s para concessões em andamento; objetos são imutáveis. Não montar URL de jogo a partir de nome ou caminho do HD. Um grant consumido/vencido responder404 corretamente; diagnóstico deve registrar rota/correlação/estado, sem registrar Bearer ou URL privada.

## Velocidade: contador não mede MB/s

O usuário esclareceu **“O contador avança de1 MB em1 MB”**. Essa atualização mostra bytes acumulados, não comprova taxa1 MB/s nem pedido de downloads paralelos. Fila de jogos foi preservada.

Medições Linux com o mesmo arquivo autorizado de65.243.904 bytes e SHA conferido:

| Caminho | Tempo | Taxa MB/s decimal |
|---|---:|---:|
| API loopback | 0,2062s | **316,351** |
| Nginx local | 0,2510s | **259,986** |
| HTTPS público, amostra1 | 17,3434s | **3,762** |
| HTTPS público, amostra2 | 14,9412s | **4,367** |
| Upload de controle16MiB | 4,4488s | **3,771** |
| Upload de controle2×16 MiB | 7,7250s | **4,344 agregados** |

Leitura/hash do disco306,33 MB/s; NIC100Mb/s FullDuplex sem erros. A taxa HTTPS medida corresponde a30,1–34,93 Mbps. **Não foi encontrado limitador artificial de bytes/s** no código, Nginx, unidade/cgroup ou cliente. **Todos os jogos, em todas as seis plataformas publicadas, usam essa mesma rota sem restrição artificial de velocidade.** Não há política de MB/s por jogo, tamanho ou plataforma. A API copia por fluxo assíncrono; o app grava/calcula hash por blocos e só sincroniza disco ao final. Limites4096capas/min por licença/aparelho,16384por origem e30autorizações/min são orçamentos de pedidos, não banda.

O caminho externo foi mais lento que API/disco/proxy; os controles de upload tiveram faixa semelhante. Isso sustenta investigar conexão de saída/provedor/túnel/rede do aparelho, sem atribuir a causa exclusivamente a um componente. Duas conexões não aumentaram a capacidade agregada além da melhor amostra única. Não há promessa de velocidade fixa para qualquer internet; o telefone não foi medido nesta rodada.

Nginx já tem `proxy_buffering off` e fluxo de artefatos sem cache público; esse comportamento encaminha a resposta ao cliente conforme chega, segundo a [documentação oficial Nginx](https://nginx.org/en/docs/http/ngx_http_proxy_module.html#proxy_buffering). Os uploads de controle usaram o endpoint documentado pelo [projeto oficial Cloudflare Speedtest](https://github.com/cloudflare/speedtest/blob/main/README.md). **Nenhuma configuração global de Nginx, NIC, kernel, Cloudflare ou cgroup foi alterada**, pois os dados não mostraram um cap local de1 MB/s.

A nova fonte do cliente mostra percentual e **MB/s real somente durante Baixando**, com relógio monotônico e média de bytes recebidos desde o primeiro evento, amostra mínima500ms. Preparação/extração não entram; retry/novo total reiniciam o medidor. Preserva ABI, fila e quatro capas. Delta de 3 arquivos sobre R11, JNI compilada e renderer R11 com sintaxe validada;9 checks de taxa e5de ZIP/BIOS passaram. A indicação ainda precisa entrar no próximo APK.

## Produção, isolamento e retorno

- API **não reiniciada**, fonte 931030bba25ca8a783f096b72dcecd26a7b49387, DLL 0b3f5da385216d216fb55220789f55c40b8eb304b7b1a4759cc154b1aa3f3ab0, PID 347227/UID 995/GID 981.
- ExecStart API: `/usr/bin/dotnet /opt/turborama-station-folders-20261004-931030b/TurboRamaSuiteOnlineServer.dll`.
- Índice ativo: `/mnt/DADOS/turbostation-library-auto-20261004/index.json`, SHA**c5cc7944ce4ad224f85e2f4218c4c91915ac6dd2bda530368554822969d86492**.
- Scanner próprio selado na fonte **cb4921431144228360a95193eeeac485cc3addc2**: `/opt/turborama-station-library-neogeo-20261005-cb49214/atualizar-biblioteca-station.py`. Somente seu ExecStart/configuração Station foram atualizados; timer de1min continua ativo.
- Novos objetos imutáveis privados: `/mnt/DADOS/turbostation-neogeo-content-20261005`; UID 995 validou índice/arquivos. Novos imports continuam pela área ativa do importador existente.
- **189 capas HTTPS** verificadas com4 workers,32.008.248 bytes em19.342,65ms;7pares de capa/download cobrem seis plataformas e ID oculto. Metadata/folderPath/assinaturas/grants/uso único/ZIP+BIOS passaram. É medição Linux, não prova de tempo no telefone.
- 12 serviços compartilhados preservaram PIDs/configuração; migrations, contas reais, chaves, contratos, registro online, APK, saves e ROMs originais intactos. Zero licenças sintéticas remanescentes. O monitor de conteúdo com falha preexistente não foi alterado.
- Testes de fonte:5 Python de descoberta/arcade/BIOS e23C# de monitor; build.NET sem warnings/erros. Não se repetiu implantação da API ou testes de outros produtos sem mudança correspondente.

Backup vigente **`/mnt/DADOS/station-neogeo-backup-20261005`**, root0700, inclui índice/revisão8, estado, configuração e unidade própria anteriores, com restauração por SHA conferida. Nenhuma alteração de schema/SQL persistente exigiu migration. Retorno específico:

```text
python3 docs/station-android/scripts/implantar-neogeo-station-20261005.py --rollback
```

Guardas exigem o índice atual conhecido e preservam API/serviços/HD. Restauram scanner/configuração/estado anteriores e republicam o conteúdo anterior em revisão **monotonicamente maior**,9→10. Os arquivos novos e a pasta Neo Geo permanecem. Se outros jogos já tiverem avançado o índice, o retorno recusa sobrescrever essa publicação; reconciliar primeiro. Rollbacks de04/10 são históricos e não devem ser executados como retorno desta entrega. Não repetir `--apply` após sucesso.

Uma tentativa anterior com fonte e3ee95d foi recusada antes de publicar o índice, após detectar CRCs inválidos; API/catalogo8 foram preservados e timer/configuração restaurados. O reparo exato da BIOS e a escrita do recibo foram corrigidos em cb49214. A tentativa bem sucedida publicou189, não190. A falha, seus recibos e backups continuam arquivados com sufixo `failed-e3ee95d`; não ocultar esse histórico.

## Próximo responsável

**App/Windows:** aplicar somente o delta de taxa sobre R11, compilar JNI e carousel com os insumos reais, preservar os DEX R10/salas e todas as demais entradas, assinatura original/alinhamento16 KiB, instalar por atualização sem limpar dados. Conferir no telefone coleções, catálogo 9 ou maior, sinopses, capas, MB/s, instalação/abertura de Neo Geo, controles/saves e retorno. R11 visual, Neo Geo gameplay, velocidade do telefone e partida entre dois aparelhos continuam sem evidência.

**Servidor:** substituir somente a origem aof2.zip por cópia válida quando disponível; acompanhar importação automática e novo catálogo. As43sinopses sem fonte estão listadas para complemento exato pelo XML/override, sem programação de jogos individuais.

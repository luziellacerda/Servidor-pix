# Novos sistemas, jogos e capas: integração concluída no Station

**SERVIDOR → PRODUÇÃO DO APP · 08/10/2026 · conclusão 23:07:38 UTC / 20:07:38 Maceió.**

Entrada única desta integração. Os cinco sistemas foram retirados de dentro de `snes` e publicados no catálogo efetivo. Nintendo **Wii** e Nintendo **Wii U** são plataformas distintas. O servidor está pronto para a leitura e o download destes itens pelo fluxo Station existente.

## 1. O que já está aplicado

| Pasta na raiz do HD | Plataforma | Novos jogos | Capas do catálogo Turborama | Capas complementares |
|---|---|---:|---:|---:|
| `gamecube` | Nintendo GameCube | 21 | 21 | 0 |
| `psx` | PlayStation 1 | 84 | 81 | 3 |
| `wii` | Nintendo Wii | 7 | 6 | 1 |
| `wiiu` | Nintendo Wii U | 1 | 1 | 0 |
| `switch` | Nintendo Switch | 1 | 1 | 0 |
| **Total** | **5 sistemas** | **114** | **110** | **4** |

- **Catálogo ativo: revisão 24, 3.848 IDs, 3.593 visíveis e 255 de compatibilidade.** Todos os 3.734 registros anteriores foram preservados integralmente: IDs, nomes, plataformas, metadados, revisões individuais, capas, artefatos e identidades online.
- As cinco pastas estão na raiz, ao lado dos sistemas anteriores. Foram preservados inode, tamanho e data dos **165 arquivos originais** durante o recorte/colagem. As pastas de organização dos jogos SNES continuam no SNES.
- Instaladas **114 capas originais em `media/revista`**, além de **279 imagens do catálogo em `media/catalogo`**, cinco `gamelist.xml` e cinco índices para associação automática. Os índices contêm **289 chaves de títulos e aliases revisados**.
- As **114 capas servidas** são JPEG **480 × 720**, qualidade 90, com proporção original preservada. Total **15.469.005 bytes**; entre 75.673 e 170.445 bytes por imagem. Todas foram comparadas byte a byte pela API pública com a versão compilada correta.
- Publicados **93.045.747.110 bytes** de artefatos dos novos jogos no próprio volume dos jogos. O disco `/mnt/DADOS` não tinha espaço para acomodar esse conjunto; o serviço recebeu um acesso somente de leitura ao novo armazenamento.
- O importador agendado já reconhece os cinco sistemas. Duas leituras completas mantiveram o catálogo idêntico; a execução do timer às 23:07:38–23:07:42 UTC terminou com sucesso.
- Permanecem ativos os **1.816 perfis online SNES/Mega**, as dez engines legadas, as licenças e as proteções existentes. Não foi alterado o APK, o túnel, o limite de velocidade de download ou o binário da API.

**Nomes, IDs, capas e arquivos de lançamento dos 114 jogos:** [novos-jogos.tsv](novos-sistemas-20261008/novos-jogos.tsv) e [novos-jogos-capas-artefatos.json](novos-sistemas-20261008/novos-jogos-capas-artefatos.json).

**Todos os nomes das 17 plataformas, incluindo os aliases:** [catalog-completo.tsv](novos-sistemas-20261008/catalog-completo.tsv) e [catálogo completo da revisão 24](novos-sistemas-20261008/catalog-completo-rev24.json).

## 2. Correspondência das capas

As 110 artes disponíveis no catálogo local do Turborama foram confrontadas com o título, a plataforma e os SHA256 dos respectivos índices de capas. É o conjunto `Assets/Catalog` e `Capas-Turborama-por-Sistema` do checkout `turborama-client-all-downloads-20260908`, com 902 itens no catálogo completo. SHA256 do catálogo de origem: `f4792aa4d52cdceb99b7f257e73bae974626ac433a8bac4d38d08920b6de7e83`.

Cada arte foi colocada no `media/revista` do seu próprio sistema, com o nome exato do arquivo de jogo sem a extensão. A capa original foi preservada; a versão leve servida ao celular foi compilada separadamente. Os dois discos de Resident Evil usam a arte do mesmo título. Variações de nome de Zelda, Sonic Heroes dublado e Donkey Kong foram conciliadas por aliases explícitos, registrados nos anexos.

Quatro jogos não estavam nesse catálogo local. Foram complementados pelo repositório de imagens Libretro da própria plataforma:

- **PSX:** Harvest Moon Back To Nature, Jackie Chan Stuntmaster BR e World Soccer Winning Eleven 2002 (Japan).
- **Wii:** Epic Mickey 2 The Power of Two.

Os endereços fixados, a árvore Git, o hash do blob e o SHA256 de cada imagem estão em [four-cover-sources.json](novos-sistemas-20261008/four-cover-sources.json). A origem, a regra de associação e o hash da imagem compilada estão registrados para **cada um dos 114 jogos**. Estas quatro imagens são complementares; não foram apresentadas como artes já existentes no site.

Foram reaproveitados **110 textos descritivos existentes**. Os quatro jogos complementares ainda têm a sinopse vazia; isso não impede sua publicação ou download. Seus IDs e a lista completa das lacunas anteriores estão em [sinopses-ainda-ausentes.json](novos-sistemas-20261008/sinopses-ainda-ausentes.json). Os textos importados não receberam revisão factual nova nesta integração.

## 3. Jogos com vários arquivos

### PlayStation 1

Os 95 arquivos de origem resultam em **84 jogos**: 76 PBP, cinco CHD e três conjuntos CUE. Os arquivos BIN/ISO referenciados por um CUE são dependências daquele jogo e não aparecem como novos jogos individuais.

| Jogo | Download entregue | Arquivo para abrir | Arquivos no pacote |
|---|---|---|---:|
| Harvest Moon Back To Nature | ZIP sem compressão | `Harvest Moon Back To Nature.cue` | 2 |
| Jackie Chan Stuntmaster BR | ZIP sem compressão | `Jackie Chan Stuntmaster BR.cue` | 2 |
| World Soccer Winning Eleven 2002 (Japan) | ZIP sem compressão | `World Soccer Winning Eleven 2002 (Japan).cue` | 10 |

As referências Windows absolutas foram convertidas em nomes relativos **dentro do pacote entregue**. Jackie Chan tinha `FILE "jackdenpsx.bin"`, enquanto o arquivo real era `Jackie Chan Stuntmaster BR.bin`; a referência foi corrigida somente no CUE empacotado. O CUE e o BIN originais no HD permanecem preservados. A regra automática aceita esse caso somente quando há um único arquivo de dados com o mesmo nome-base exato do CUE.

O app deve extrair o conjunto completo e abrir `artifact.launchPath`. Abrir apenas o BIN ou descartar as faixas de áudio perde parte do jogo.

### Nintendo Wii U

Mario Kart 8 havia chegado com apenas 41 arquivos. A cópia completa do mesmo jogo já existia em outro HD conectado ao servidor. Os 41 arquivos existentes foram confrontados byte a byte, e foram acrescentados **3.009 arquivos ausentes**, sem sobrescrever os existentes.

O conjunto publicado tem **3.050 arquivos**, com `code`, `content` e `meta`, e **4.316.310.053 bytes** descompactados. O download é um ZIP64 sem compressão de **4.316.896.523 bytes**. Seu lançamento é **`code/Turbo.rpx`**. O app deve manter a árvore inteira após extrair; o RPX isolado é insuficiente. A estrutura necessária também está descrita na [documentação do Cemu](https://wiki.cemu.info/wiki/Serfrosts_Cemu_Setup_Guide).

### GameCube, Wii e Switch

Os arquivos ISO/GCM/RVZ e NSP presentes são entregues como `raw`, com os bytes originais. Não foram convertidos em outro formato nem recomprimidos. GameCube e Wii são plataformas atendidas pelo Dolphin, conforme sua [documentação oficial](https://dolphin-emu.org/docs/faq/); isso não constitui um teste de execução nos telefones desta instalação.

## 4. Como o app deve ler a produção

**Base pública: `https://app.lzgames.com.br`.** Usar a sessão Station, a prova de requisição e a verificação do envelope já implementadas. Os caminhos do HD não fazem parte da API pública.

1. **Catálogo:** `GET /v1/station/catalog?metadata=1`. A revisão global atual é **24**. Esperar **3.593 itens visíveis**; os 255 aliases de compatibilidade constam do export administrativo, não da lista pública. Reconstruir metadados quando mudar a revisão global, mesmo quando a revisão individual do jogo continua igual. Sem `metadata=1`, a sinopse é omitida por contrato.
2. **Plataforma:** usar o `platform` exato do item. `gamecube → GameCube/gamecube`, `psx → Playstation 1/psx`, `wii → wii/wii`, `wiiu → wiiu/wiiu`, `switch → Switch/switch` são os mapeamentos já presentes em `StationPlatforms.java` na fonte R81. Wii é Nintendo Wii; Wii U usa outro sistema e outra pasta. A publicação destes IDs não exige programar cada jogo ou cadastrar novas aliases no APK R81.
3. **Capa:** `GET /v1/station/covers/{coverId}`, com o `coverId` recebido para aquele `itemId`. Cache por `coverId` e revisão individual. A fila R81 já tem **quatro workers de IO**, com reposição imediata. A ordem visual não deve ser usada para associar capa e jogo.
4. **Download:** `POST /v1/station/downloads/authorize` com o `itemId` exato no envelope vigente. Usar o descritor assinado recebido: `fileName`, `format`, `launchPath`, `fileCount`, `sizeBytes` e `expandedSizeBytes`. Autorizar um item não autoriza outros arquivos ou rotas do servidor.
5. **Arquivo:** consumir `GET /v1/station/artifacts/{grantId}` com a mesma sessão e prova, dentro do prazo do grant. Ele é de uso único; um grant já consumido retorna 404. Para outra tentativa, emitir outro grant. O conteúdo deve ter o tamanho do descritor e `Content-Type: application/octet-stream`.
6. **Instalação:** em `raw`, manter o arquivo original. Em ZIP, extrair todos os membros e preservar o caminho completo de `launchPath`, inclusive `code/Turbo.rpx`. O modelo R81 usa `long` e aceita estes tamanhos, ZIP64 e até 100.000 arquivos. Mario Kart 8 exige espaço para o conjunto de 4,02 GiB descompactado e, conforme o fluxo de staging do cliente, também para o download temporário.
7. **Atualização automática:** a R81 consulta o catálogo a cada 60 segundos quando está em primeiro plano e sem downloads/capas ativos. A consulta inicial automática tem espera de cinco segundos. Também existe a ação de atualizar catálogo. Não é preciso gerar um APK por causa destes 114 cadastros.
8. **Motor e online:** a resolução da plataforma e o download estão prontos. A execução local ainda depende do emulador instalado no aparelho. GameCube/PSX/Wii/Wii U/Switch não foram acrescentados ao manifesto de motores online; os **1.816 perfis SNES/Mega existentes** continuam intactos. Não mostrar estes novos jogos como salas online disponíveis por inferência do catálogo ou da sinopse.

Referência conciliada: TurboElden `482fcc0069bd414761311a0b99cf806600a82f7c`, conjunto Java `versions/station-online-readiness-r81-20261008/java/`. Foram lidas `StationPlatforms`, `StationApi`, `StationCatalog`, `StationFrontend`, `StationCoverQueue`, `StationCoverStore`, `StationArtifact` e `StationInstaller` nessa fonte. Nenhum APK foi montado ou instalado no Linux por esta integração.

## 5. Colocar outro jogo e reconhecer automaticamente

O timer Station já está ativo para os 17 IDs de plataforma do catálogo. Para **estes sistemas cadastrados**, basta colocar os arquivos na pasta correta:

- Um jogo GameCube/Wii/Switch pode ser copiado em sua pasta ou em uma subpasta de coleção. O importador descobre o arquivo pela extensão e publica o nome e a pasta automaticamente.
- Um jogo PSX em CUE precisa chegar com todas as faixas referenciadas. Um PBP/CHD é um arquivo único. As faixas do CUE não viram itens separados.
- Um Wii U extraído precisa ter a árvore `code/content/meta`; não copiar somente o RPX. WUD/WUX/WUA e arquivos compactados também têm tratamento na configuração do sistema.
- A varredura ocorre aproximadamente a cada minuto. Um novo conjunto precisa ser observado em **duas varreduras estáveis**, com pelo menos **20 segundos** desde a última alteração. A compilação inicial de arquivos grandes pode levar tempo; os pedidos posteriores de download leem o artefato já pronto.
- A capa original pode ser colocada em `media/revista`, com o **mesmo nome-base do jogo** e extensão JPG/PNG/WebP. No Wii U, o nome-base segue o RPX; para Mario Kart 8 a associação exata instalada é `Turbo.jpg`. Se houver correspondência nas **289 chaves locais** do índice, a capa e a descrição disponíveis são associadas automaticamente dentro da mesma plataforma.
- Quando não houver capa conhecida, o jogo pode entrar com capa pendente. Colocar a imagem correta na pasta atualiza o próprio item, preservando seu ID. Não é necessário modificar o código do importador por jogo.
- Uma sinopse manual pode ser escrita no `gamelist.xml` da plataforma. Texto manual existente prevalece sobre o índice de associação. O override privado do operador continua disponível; não se deve editar diretamente o índice compilado.

Exemplo de metadados para um arquivo novo:

```xml
<gameList>
  <game>
    <path>./Minha coleção/Meu jogo.chd</path>
    <name>Meu jogo</name>
    <desc>Sinopse escrita pelo operador.</desc>
  </game>
</gameList>
```

Acrescentar a entrada ao XML existente, preservando as demais. A cópia do XML instalado e dos cinco índices está em [metadata/](novos-sistemas-20261008/metadata/).

As extensões atuais dos cinco sistemas constam do importador/configuração e incluem ZIP/RAR/7z. Um arquivo compactado precisa identificar um jogo de lançamento; casos ambíguos exigem `launchPath` explícito nos metadados do operador. Um sistema novo ainda não cadastrado exige registrar **a plataforma**, não programar cada título. O limite atual do índice é 4.096 IDs; restam 248 antes de uma ampliação coordenada com os clientes.

## 6. Produção e evidências

| Componente | Efetivo após a integração |
|---|---|
| API Station | `turborama-station-api.service`, PID **1575654**, NRestarts **0** |
| ExecStart da API | `/usr/bin/dotnet /opt/turborama-station-online-r81-20261008-db50a98/TurboRamaSuiteOnlineServer.dll` |
| SHA256 da DLL preservada | `cdf14b8067de415413c503de787c6d621c6e8f0466eb2cdd7c0eced8b5a619af` |
| Importador efetivo | `/opt/turborama-station-library-new-systems-20261008-12f0475e90/atualizar-biblioteca-station.py` |
| SHA256 do importador | `4ac58964a9683d41e5a35275a20dbd4c40a666b87469de3253c74def37a3f4fe` |
| SHA256 do índice privado rev24 | `6b8acfa4ca419ec705f48f53e2063633ff8f0a30a36cb8f4d651a108cbb31137` |
| Timer | `turborama-station-library-scan.timer`, ativo; última execução observada sucesso/status0 |
| Conteúdo novo | Acesso somente de leitura na sandbox da API; a identidade real do serviço abriu todos os **3.848 jogos e capas** |
| Online | **1.816 perfis preservados**, flags R81 e dez engines legadas preservadas; nenhuma sala/conexão residual ao concluir |
| Outros serviços | PIDs dos nove serviços compartilhados conferidos antes/depois, preservados |

Testes específicos e regressões: **30 casos passaram**, sendo seis de conjuntos PSX/Wii U/associação automática, cinco do importador, quatro de identidade online e 15 de revista. Os hashes das ferramentas estão em [tested-tools.json](novos-sistemas-20261008/tested-tools.json).

Prova externa **autenticada e assinada**, pelo domínio público:

- Catálogo normal e `?metadata=1`: todos os **3.593 IDs visíveis**, revisão, metadados e identidades coincidiram com o índice aplicado; nenhum caminho privado foi retornado.
- **114 capas**, comparadas byte a byte, com **quatro requisições simultâneas**.
- Um grant e stream representativo por sistema: tamanho anunciado correto, prefixo de 64 KiB idêntico ao arquivo publicado e segunda utilização recusada com 404. Para Wii U, foram conferidos o ZIP de 4,02 GiB e o descritor de **3.050 arquivos**.
- Fixture sintética própria removida do banco. Duas varreduras completas não mudaram o índice; não houve pendência nova nos cinco sistemas.

Isso comprova publicação, associação das capas, autorização, estrutura dos pacotes e entrega inicial do stream. **Não foi medido um download completo de vários GiB nem executado gameplay nos telefones** nesta rodada. Não anunciar ganho de MB/s ou estabilidade de jogo online com base nestas conferências.

### Pendência anterior preservada e histórico da ativação

O Neo Geo já registrava `# 0 - ART OF FIGTHERS COLEÇÃO #/aof2.zip` com `ValueError` antes desta integração. O importador continua conservando seu último registro válido; essa pendência não pertence aos cinco novos sistemas. Também existem 65 referências antigas de XML sem ROM e a ausência de firmware Neo Geo CD já registrada no histórico. Não foram apagadas ou disfarçadas como resolvidas.

A primeira preparação reteve o CUE incorreto de Jackie antes de publicar. A primeira ativação foi revertida automaticamente porque a checagem tratou a pendência antiga do Neo Geo como nova. O catálogo voltou à revisão23 com os 3.734 registros originais idênticos. A checagem foi corrigida para confrontar o baseline e executada **antes** da ativação final; os 114 artefatos preparados foram reaproveitados. A publicação final é a **revisão24** e passou nas provas públicas.

Após o mantenedor confirmar a saída nos dois celulares, uma sala v3 vazia, sem conexões ou bytes retidos, permaneceu registrada. Ela foi encerrada na primeira reinicialização. Houve três reinicializações administrativas limitadas à API Station nesta tarefa, contando a primeira troca, seu retorno e a troca final; não houve partida com conexões ativas nessas trocas. NRestarts0 é o contador de reinícios automáticos da instância atual, não prova de que não houve reinicialização administrativa.

O backup protegido da ativação final é `/mnt/DADOS/station-new-systems-backup-20261008-200657`; os recibos detalhados e a configuração ficam na área privada do operador. Os operadores históricos e o operador desta primeira aplicação têm guardas de baseline; **não reaplicá-los à release ativa**. Para retorno futuro: conferir versão/índice/PID, ausência de partidas, restaurar somente os drop-ins/configuração/estado próprios a partir do backup e manter a revisão do catálogo monotônica. Não restaurar banco ou excluir ROMs para voltar o cadastro.

## 7. Uma entrega para concluir a leitura no app

1. Usar [summary.json](novos-sistemas-20261008/summary.json), o catálogo completo e o inventário dos 114 novos itens para comparar a leitura do aplicativo com a revisão24.
2. Atualizar o catálogo pelo fluxo já existente. Conferir os cinco sistemas, os nomes e as capas pelos IDs, mantendo Wii e Wii U separados.
3. Baixar um item de cada formato usado; para PSX, conferir CUE junto das faixas; para Wii U, preservar `code/content/meta` e abrir `code/Turbo.rpx`. O servidor já está aplicado; não repetir sua implantação no PC do APK.
4. Registrar em um único retorno o resultado observado nos aparelhos. Qualquer falta de emulador/execução local deve trazer o sistema e o erro; qualquer divergência de imagem deve trazer `itemId`, `coverId` e revisão recebidos. Não pedir ao mantenedor outro índice privado, senha ou hash que já conste desta entrega.

Os anexos são inventário e evidência sanitizados. O catálogo JSON do pacote contém descritores auxiliares; **não é um envelope assinado nem substitui a API**. Para dados atuais, continuar usando as rotas protegidas da produção.

O fechamento anterior de modos/controles de 1 a 4 pessoas continua em [HANDOFF-UNICO-FECHAMENTO-ONLINE-1A4-STATION-20261008.md](HANDOFF-UNICO-FECHAMENTO-ONLINE-1A4-STATION-20261008.md). Seus números de catálogo/release são snapshots anteriores; esta entrega atualiza o catálogo e o importador, mantendo as tarefas de motores e controles ali identificadas.

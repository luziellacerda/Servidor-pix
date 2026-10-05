# Neo Geo CD organizado e publicado — 05/10/2026

## Produção

**Catálogo 14: 2.212 jogos visíveis, 255 IDs de compatibilidade, 2.467 internos.** Neo Geo CD acrescenta **50 jogos, 50 capas exatas da revista e 50 sinopses**. Total de sinopses: 2.169; as 43 pendências anteriores permanecem sem fonte. Todos os 2.417 registros internos anteriores, inclusive os 996 IDs originais, foram preservados exatamente.

| Plataforma publicada | Jogos |
|---|---:|
| SNES | 644 |
| SNES BR | 191 |
| Mega Drive | 887 |
| Mega Drive BR | 94 |
| Nintendo 64 | 157 |
| Neo Geo | 189 |
| Neo Geo CD | 50 |

A pasta solicitada permanece **`neogeo/neogeocd`**, dentro da pasta Neo Geo na raiz do HD. Não houve movimento dos arquivos CD para outra raiz. O scanner exclui a plataforma filha da leitura do Neo Geo comum, evitando cadastrar BIOS/pacotes CD como arcades.

## Discos, capas e dados

Os 50 arquivos de jogos recebidos têm extensão `.img`, mas o conteúdo é **CHD v5**, não imagem RAW de CD. Todos passaram na verificação integral de `chdman verify` 0.264, sem `--fix`. A entrega recebe extensão `.chd`, com os mesmos bytes e SHA256. Não há conversão ou recompressão; CHD já é comprimido. O arquivo original e a referência XML `.img` permanecem no HD.

Descritor atual: `format=raw`, `fileName=launchPath=<nome>.chd`, `fileCount=1`, `expandedSizeBytes=sizeBytes`. O app deve usar o descritor assinado, não a extensão da origem nem o tamanho lógico do disco CD. Nenhuma BIOS foi embutida nessa entrega.

Cada jogo foi associado à revista de mesmo stem exato em `media/revista`, origem 1024×1536. A cópia servida é JPEG 480×720, qualidade90, mantendo a arte. `media/images` não substitui a revista. Os exports registram separadamente SHA da revista original e SHA da capa compilada. As 50 descrições e os outros metadados foram lidos do XML correspondente.

O catálogo completo e a lista CD estão em [biblioteca-neogeocd-20261005](biblioteca-neogeocd-20261005/README.md), sem caminhos internos de ROM/URLs/segredos. TSV usa tabulação e descrições multilinha; ler com parser CSV.

## BIOS e execução Android

O usuário informou que **não possui BIOS Neo Geo CD**. A falta não bloqueia catálogo, capa, sinopse ou download. A BIOS Neo Geo MVS não substitui o firmware CD.

Para o servidor, colocar uma BIOS CD própria em `neogeo/neogeocd/bios` ou `neocdz.zip`/`neocd.zip` na raiz CD. O importador aceita somente conteúdo com tamanho/CRC/SHA1 das identidades do driver `neocdz`, incluindo o auxiliar `000-lo.lo`; este auxiliar pode ser reaproveitado do `neogeo.zip` já existente. Firmware não vira jogo. Não há busca/baixador de BIOS. Enquanto faltar, o relatório privado registra `runtimeRequirements.neogeocd.biosAvailable=false`.

Quando houver firmware válido, a observação estável muda o fingerprint e recompõe a entrega automaticamente: ZIP_STORED externo com `<nome>.chd` e `neocdz.zip` fechado, `launchPath=<nome>.chd`, `fileCount=2`. Mantém o ID, aumenta a revisão do item e preserva entregas anteriores. O app extrai o pacote externo uma vez.

**Retorno novo do aplicativo incorporado:** [`9547071`](https://github.com/luziellacerda/TurboElden/tree/954707196be089969b723e16f68cccaa0c48dea2) comprova R18 instalada, APK `a29151da312830d826f6ea71ebb61cb8a39fe1c21786f719e26a61568b29b1c4`, com modo filesystem e rompath do MAME, navegaçãoR17, desempenho/offlineR16 e N64 completo. SVC Plus/controles receberam prova na R18; NeoCD ainda não recebeu prova no telefone.

A [ponte NeoCD e receitas](https://github.com/luziellacerda/TurboElden/tree/5dea14c8b361ec6f0a8fb6c8a1ac92720d27542a/versions/station-neogeocd-20261005), commit **5dea14c8b361ec6f0a8fb6c8a1ac92720d27542a**, foi conciliada com todos os novos commits do retorno, compilada em Java/DEX e testada. Mantém o bootstrap R18 exatamente igual. Inclui seletor Android **IMPORTAR BIOS**, identificação exata e armazenamento privado, preparação do driver ao lado do disco, e abertura com:

```text
ACTION_VIEW → neocdz.zip
cli_params = -rompath '<pai real da instalação>' -cdrom '<CHD absoluto>' -bios official
```

O tokenizer do donor exige **aspas simples**. `PREF_ROMsDIR_2` permanece string vazia como na R18; preencher essa preferência com o caminho físico ativaria SAF sem URI. Neo Geo comum mantém seu rompath anterior. Os seis argumentos foram conferidos com o parser nativo e nomes contendo espaços. Alternativas BIOS33/32 também são reconhecidas por identidade.

**Não há APK CD novo assinado/instalado nesta entrega.** Linux sem base APK privada, certificado ou aparelho. DEX compilado SHA `4cd9394219e7da86be9ab249d24a2c40862592f9e4595968769b8ce79fbfd6d9`; 25checksJava e overlay2→2→0 passaram. Empacotar somente `classes30.dex` sobre a R18 exata, preservar as demais entradas, alinhar/assinar pelo fluxo canônico e instalar por atualização. Não retroceder para R15/R16 nem reaplicar empacotadores antigos. Não reintroduzir hashing do corpo dos jogos removido por pedido do usuário na R16; hash de netplay/build continua.

## Como o app consome a produção

Base: `https://app.lzgames.com.br`, sessão Station existente e contrato assinado RSA-PSS com identidade/autoridade validadas.

| Operação | Informação correta |
|---|---|
| `GET /v1/station/catalog?metadata=1` | `platform=neogeocd`, itemId/nome/revisão/coverId, folderPath e metadata |
| `GET /v1/station/covers/{coverId}` | Capa compilada correspondente ao jogo; quatro workers reaproveitados ao terminar cada pedido |
| `POST /v1/station/downloads/authorize` | `itemId` e identidade da sessão; verificar grant e descritor assinados |
| `GET /v1/station/artifacts/{grantId}` | Corpo binário da mesma sessão; uso único; instalar conforme artifact.fileName/format/launchPath |

`neogeocd` já tem alias para **Neo Geo CD**, pasta Android `neo-geo-cd`; não precisa acrescentar cada título ao APK. A resposta do artefato não depende de `Content-Disposition` para nomear a instalação: o nome correto vem do descritor assinado. Não montar URLs com o nome do jogo, caminho do HD ou arquivo da revista. As rotas de capas/artefatos exigem autorização; um grant consumido retorna404 e precisa de autorização nova na repetição.

O cliente consulta o catálogo em primeiro plano, autorizado e sem download ativo. Offline usa o catálogo assinado já salvo conforme R16; um catálogo2162em cache não comprova o total2212da rede. Revisão global14 é distinta da revisão individual dos CD10. Aumentar revisão global não altera os recibos dos jogos anteriores.

Todos os jogos continuam pela mesma rota de download, **sem limitador artificial de MB/s**. Quatro capas concorrem; downloads mantêm a fila do cliente. O servidor não foi configurado com pacing de bytes. Limites de pedidos/permissões não são limitação de velocidade de um corpo autorizado.

## Implantação e evidência

- API mantida: fonte931030b, DLL `0b3f5da385216d216fb55220789f55c40b8eb304b7b1a4759cc154b1aa3f3ab0`, PID347227, UID995/GID981.
- Scanner fonte **9cff9b333b5e0fcaec6d8a12f75b61519d8c7018**, release `/opt/turborama-station-library-neogeocd-20261005-9cff9b3`; quatro módulos/verificador privado selados porSHA. Nenhum pacote global instalado.
- Índice14 SHA `07ad4c3fda41a19c23745436c4c45c97a70b2c7688eff788612fe22743823a92`; timer por minuto, duas observações estáveis/idade20s, autoReload10s. Disco grande acrescenta tempo de validação. API manteve o último catálogo válido durante as verificações.
- Leitor real validou os50CD sob UID/GID do serviço; import/reimport não mudou registros anteriores. O recibo de verificação integral foi reaproveitado somente na preparação inicial após SHA completo coincidir; não foi gravado na configuração ativa. Novos arquivos recebem verificação integral.
- Oito pares capa/download HTTPS, catálogo/grants/RSA/identidade/folderPath/metadata/compatibilidade e uso único passaram.
- **50 capas / 9.239.550 bytes / 5.449,87ms / 4workers**, todas iguais ao objeto compilado. Amostra CD `Magical Drop II` recebeu CHD completo de334.021bytes com SHA do descritor conferido. Isso mede a verificação no servidor, não a rede do telefone.
- 12 serviços compartilhados/PIDs preservados; Nginx/Cloudflare/NIC/kernel/bancos/chaves reais inalterados. Zero registros sintéticos remanescentes. Online já ativo foi preservado; relay R12 continua entrega separada, sem implantação nesta rodada.
- Primeira verificação automática consultou rápido demais e recebeu429; rollback correto levou10→11. A segunda esperava um Content-Disposition que não faz parte do contrato, levando12→13. Ajustes respeitaram o intervalo e o descritor; publicação14 concluída. Backups/recibos das tentativas foram preservados, arquivos verificados reutilizados; não repetir`--apply`.

## Operação, retorno e próximas conferências

Backup vigente **`/mnt/DADOS/station-neogeocd-backup-20261005`**, índice13/configuração/estado/unidade anteriores. Conteúdo CD em `/mnt/DADOS/turbostation-neogeocd-content-20261005`. Retorno específico:

```sh
python3 docs/station-android/scripts/implantar-neogeocd-station-20261005.py --rollback
```

Guarda SHA atual; catálogo anterior reapublicado em revisão superior14→15, API sem reinício, conteúdos e originais preservados. Se imports posteriores avançarem o índice, recusa descartá-los; reconciliar. Backups NeoGeo/04 são históricos. Retorno não pode ser confundido com apagar50arquivos.

Provas privadas600: `station-neogeocd-rollout-result-20261005.json`, `station-neogeocd-export-private-20261005`, `station-neogeocd-check-20261005` com recibos/verificação sem senhas. No HD há guia `LEIA-ME-STATION-NEOGEO-CD.txt` e pasta`bios` para futura BIOS própria.

Falta externa: BIOS válida e APK com a ponte CD sobreR18, atualização/hash no Android, dois discos CD, áudio/controles/saves/retorno e troca paraNeoGeo/N64. A catalogação dos próximos CHD/capas/XML dispensa programação por jogo; o motor precisa deste ajuste uma vez. Corrupção anterior de `aof2.zip` NeoGeo segue pendente; o disco CD Art of Fighting2 é outro arquivo e passou na validação.

## Referências técnicas

[CHD/MAME](https://docs.mamedev.org/tools/chdman.html), [driverCDZ](https://github.com/mamedev/mame/blob/master/src/mame/snk/neogeocd.cpp), [intent MAME4droid](https://github.com/seleuco/MAME4droid-Current/blob/main/android-MAME4droid/app/src/main/java/com/seleuco/mame4droid/Emulator.java), [parserCLI](https://github.com/seleuco/MAME4droid-Current/blob/main/src/osd/myosd/droid/myosd_droid.cpp). A interface foi conferida também no retorno do donor exatoR18; não trocar o motor por outro download.

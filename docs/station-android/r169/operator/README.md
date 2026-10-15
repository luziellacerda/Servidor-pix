# Preparador R169 sobre a R168 aplicada

A autoridade R168 foi aplicada no servidor segundo
[`server-return.json`](../../r168/server-return.json): 91 motores, 16.896 perfis,
11.981 modos, catálogo revisão 100 com 4.744 itens. O limite WinBack por conteúdo
foi corrigido para 256 KiB e validado nesse ciclo. O Android R169 instalado em
dois telefones ainda recebe, para *Capcom vs. SNK — Millennium Fight 2000 Pro*,
“não há modo aprovado para esta edição e o motor instalado”. A R169 **não foi
cadastrada** no último estado remoto confirmado.

**Bloqueio de capacidade antes do cadastro:** o pré-voo R168 registrou
16.762.688 bytes para os perfis formatados (teto antigo 16.777.216) e
8.126.781 bytes para os modos compactos (teto antigo 8.388.608). A adição
R169 de 492 registros equivalentes excede ambos os tetos antigos. Esta branch
alinha produtor, registrador transacional, importador e leitor C# em **17 MiB
para perfis** e **9 MiB para modos**. O pré-voo R169 deve registrar os tamanhos
exatos compactos/formatados e rejeitar qualquer excesso. A reserva solicitada
para motores futuros dobra o manifesto v3 de **128 motores/512 KiB para 256
motores/1 MiB**; somente quatro motores reais são acrescentados nesta revisão,
chegando a 95. O registro legado v1/v2 de 32 entradas não é esta autoridade e
não foi alterado. Nenhum limite por jogo ou aprovação foi relaxado.

`prepare_all.py` é apenas um preparador local, sem rede ou implantação. Recebe
o ZIP privado R169 de hash SHA-256
`859b33b156698abecadadf035f8117a4b2b680439c4fed32eb2001347dc6c832`
e os quatro registros **atuais** exportados da autoridade R168: catálogo,
perfis, modos e manifesto de motores. Confere cada arquivo do kit, prova do APK
assinado, os quatro novos `engineId`, política nativa, perfil de controles e
vínculos existentes. Recusa qualquer alteração na edição, conteúdo, core,
opções, aprovações e baseline. Prepara 492 modos/perfis aditivos: 93 PSX
(23 VS/2P aprovados, 17 pendentes, 53 classificações locais), 387 N64,
9 GameCube e 3 Wii. Nenhum perfil ou modo anterior é removido ou modificado.
O N64 preserva a lógica existente; somente a nova identidade vinculada ao APK
ganha registro próprio.

O preparador exige os quatro SHA-256 **registrados pela autoridade R168** para
catálogo, perfis, modos e manifesto. Obtê-los dos recibos privados
`/mnt/DADOS/station-r168-capacity-20261015/completed.json`,
`deployment-applied.json` e `current-applied-audit.json`, reconciliando as
entradas entre eles antes de iniciar. Copiar os quatro arquivos atuais sem
reescrever JSON e comparar seus hashes com esses recibos. Se faltar um hash
autoritativo ou houver divergência, interromper e reconciliar a R168; calcular
quatro hashes novos apenas dos próprios arquivos de entrada R169 não comprova
a baseline. O preparo só escreve em um diretório novo, vazio e real; symlinks
são recusados.

## Execução no servidor

Usar os arquivos reais da autoridade aplicada, identificados pelo operador a
partir de `/mnt/DADOS/station-r168-capacity-20261015/completed.json` e da
configuração ativa. Não usar cópias do Git como se fossem o estado atual. Criar
um diretório de **preparação** novo antes do comando. Nenhum argumento de saída
pode apontar para um registro ativo ou arquivo de entrada.

Antes do preparo/registro, instalar e conferir pelo hash as cópias **ativas**
de `station_online_profiles.py`, `cadastrar-motor-online-station.py` e
`atualizar-biblioteca-station.py` em seus respectivos diretórios protegidos.
A manutenção R168 comprovou o scanner em
`/opt/turborama-station-library-quarantine-20261014-a10c78f53797/library-tools/atualizar-biblioteca-station.py`.
Um recibo anterior mostrou o registrador sob
`/opt/turborama-station-endpoints-20261010-.../library-tools/`; resolver o
caminho **efetivo atual** pela configuração/comando do servidor antes de usar
`prepare` ou `apply`. O registrador deve importar no seu próprio diretório o
`station_online_profiles.py` corrigido, com os mesmos tetos 9/17 MiB e 256
motores/1 MiB. O scanner deve importar sua cópia correspondente. As cópias
históricas sob `docs/station-android/scripts/` não são o caminho de implantação.
Conferir
também que o serviço carrega a versão correspondente de
`StationMultiplayerProfileMonitor`/`StationOnlineRegistration` com teto de
17 MiB **se** `profilesCompactBytes` do pré-voo ultrapassar 16 MiB. Se o
arquivo compacto permanecer dentro do teto antigo, o cadastro R169 não exige
reiniciar o serviço apenas por esta ampliação; o novo binário pode seguir na
janela de manutenção, preservando as partidas N64 em andamento. Nunca publicar
um registro que o leitor ativo rejeitará. Publicar binário e registries como
conjunto compatível quando necessário, com backup e rollback transacional; o
checkout Git sozinho não altera o processo em execução.

```text
python3 docs/station-android/r169/operator/prepare_all.py \
  --kit-zip <ZIP-R169-recebido-e-conferido> \
  --r168-receipt docs/station-android/r168/server-return.json \
  --catalog <catalogo-atual-exportado> \
  --profiles <perfis-atuais-exportados> \
  --modes <modos-atuais-exportados> \
  --manifest <motores-atuais-exportados> \
  --r168-catalog-sha256 <hash-autoritativo-r168-do-catalogo> \
  --r168-profiles-sha256 <hash-autoritativo-r168-dos-perfis> \
  --r168-modes-sha256 <hash-autoritativo-r168-dos-modos> \
  --r168-manifest-sha256 <hash-autoritativo-r168-do-manifesto> \
  --out-manifest <preparacao-nova>/manifest.json \
  --out-profiles <preparacao-nova>/profiles.json \
  --out-modes <preparacao-nova>/modes.json \
  --out-audit <preparacao-nova>/preflight.json
```

Confirmar `enginesAdded=4`, `profilesAdded=492`, `modesAdded=492`, prefixos
anteriores idênticos, 23 aprovações PSX/2P e `capcomVs2pProfileId` igual a
`psx-r169-20261015-005-versus`. Conferir `modesCompactBytes <= 9.437.184`,
`profilesIndentedBytes <= 17.825.792`, `manifestCompactBytes <= 1.048.576`
e os SHA-256 da saída contra `candidateSha256` no pré-voo. Os três registros
preparados são serializados no formato compacto do escritor transacional,
sem troca de campos ou hashes de conteúdos. Em seguida usar o mecanismo
transacional `library-tools/cadastrar-motor-online-station.py prepare` com
`incoming.json`, ELF reais e `--mode-additions` apontando para os modos
preparados; conferir seu recibo e só então executar `apply` com a configuração
ativa e autenticação administrativa nativa. Nenhum desses comandos é executado
por este handoff. Usar o mecanismo transacional e
as verificações de publicação já empregados na R168. Este preparador não
substitui essas etapas. Não publicar um conjunto se qualquer preflight falhar.

O registrador histórico ainda possui fallback de aprovação genérica quando o
metadado de jogadores não é `1`. Este trabalho preserva seu comportamento
anterior para não alterar modos existentes sem prova. Os quatro novos motores
R169 exigem modos explicitamente vinculados (`requireExplicitEngineModes`);
nenhuma aprovação de dois jogadores é inferida do metadado. Depois do registro,
executar o [auditor independente das 4.744 edições](https://github.com/luziellacerda/Servidor-pix/tree/db348905ebe8b0a363ee67fe7abec0552dc78903/docs/station-android/r169/coverage-audit)
com os quatro exports atuais. Ele separa aprovações comprovadas, classificações
locais, recusas e aprovações genéricas sem proveniência. Essa auditoria é
somente leitura e não bloqueia o cadastro exato do piloto Capcom VS.

Após a publicação efetiva, emitir recibo `serverApplied=true` com hash do kit,
contagens, quatro IDs, versão do serviço e consulta pública **autenticada e
assinada** das capacidades da edição exata `station_767e5e92f2f353479e6120f1c28f96a7`:
conteúdo SHA-256 `6782ccca63a19cbc6760ca928b3c36d0e6ac4b3e479c722083c96111bd25cc73`,
PSX R169, VS, duas vagas. Só então executar a partida física entre os dois
telefones. `HTTP 200`, sala `playing` e simulações não provam imagem, áudio,
controles nem saída do anfitrião.

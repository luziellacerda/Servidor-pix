# Segundo jogador — entrada na mesma sala — 05/10/2026

## Uso imediato nos APKs atuais

**Estou pronto confirma quem já está na sala atual. A entrada na sala do primeiro telefone precisa acontecer antes.** Se cada telefone criou uma sala, ambos podem marcar Pronto e continuar sozinhos.

1. No POCO, tocar **Sair da sala**, mantendo a conta.
2. No primeiro telefone, manter a sala aberta e usar **Código da sala → Copiar código**.
3. No POCO, abrir **Código** no cabeçalho do lobby, colar o código TS1 e tocar **Entrar na sala**. TS1 identifica a sala; o código de ativação da licença tem outra função.
4. Conferir os **dois nomes na mesma sala**. A mesma edição do jogo deve estar baixada nos dois telefones.
5. Ambos tocam **Estou pronto**; só quem criou a sala usa **Iniciar partida**. O convidado abre o jogo automaticamente quando o anfitrião confirma que o motor está escutando.

Se o jogo ainda falta, baixar a edição indicada no catálogo e repetir a entrada. Para participar da sala do primeiro telefone, usar Entrar em vez de Criar sala no POCO.

## Último retorno do aplicativo conciliado

O retorno **a8898a0** confirma **R34 instalado no Samsung às20h08 de05/10 (UTC−3)**, por atualização, com hash integral conferido e sessão mantida. A versão instalada no POCO continua sem conferência. Esta entrega recompila a correção sobre a fonte exata do R34 e preserva recuperação de abertura, logs, callback Voltar, booleano do manifesto e design R33.

A compilação inicial sobre R30 foi substituída: **não usar o DEX ef8a2d99 ou o APK R30 para esta atualização**. Os snapshots publicados R31/R33/R34 foram mantidos; seus80 hashes de arquivos foram conferidos na conciliação.

## Relato e evidência

O mantenedor informou: primeiro telefone mostra apenas um participante; no segundo a sala aparece sem Entrar; tocar Estou pronto não o reúne ao primeiro.

**Demonstrado nas fontes R30 e R34 anteriores a este delta:**

- O perfil do anfitrião mostra “Em uma sala” e desabilita Convidar, sem oferecer Entrar.
- Quando existe sala própria, a tela oculta a lista das outras salas públicas. Dois anfitriões sozinhos não conseguem escolher ali a sala um do outro.
- `ready` atualiza a confirmação na sala atual; `join` exige primeiro deixar qualquer outra sala.

Duas salas individuais são consistentes com o relato. Um teste isolado usando a lógica real do servidor reproduziu: dois `create` geram IDs distintos; Pronto mantém um participante; `leave` seguido de `join` reúne os dois. **Sem snapshot autenticado ou captura desses dois Android neste Linux, a identidade exata das salas observadas permanece sem prova.**

Registros públicos entre22h35–22h52UTC mostram `/online/command`200 e dois upgrades101 separados, de8s/60s, sem aumento dos bytes do relay. Status e tamanho do corpo não identificam ação ou telefone; não inferir gameplay dessas respostas. Houve409/499 em eventos, sem corpo/código suficiente para atribuir causa específica. Veja o diagnóstico sanitizado anexo.

## Correção entregue

Fonte compilada: **`f7f056125ad208d8a0e139b392d382d68f61787f`** no TurboElden, ramo `fix/station-second-player-20261005`, diretório `versions/station-second-player-20261005`. A fonte completa R34 foi restaurada e conferida pelo SOURCE-MANIFEST; **144 de147 entradas Java/dependências preservadas exatamente**. Apenas três classes mudam:

| Arquivo | Mudança |
| --- | --- |
| `StationPlayerModel.java` | Oferece Entrar usando `roomId/itemId` publicados; distingue sala própria individual, já compartilhada, cheia e em partida. |
| `StationPlayerSheet.java` | Perfil do anfitrião oferece **Entrar na sala**, habilitado pelo snapshot verificado. |
| `StationRoomsActivity.java` | Mostra **Outras salas com vaga** quando há sala própria individual; exibe contagem1/2 e orientação sobre Pronto; confirma **Sair e entrar**, prepara o jogo antes da saída e usa `leave/join` existentes. Preserva as mudanças R34. |

Jogo ausente ou motor incompatível falham antes de deixar a sala própria. A troca é oferecida quando a sala própria aguarda com o usuário sozinho. A confirmação informa que essa sala individual será encerrada. Uma sala em partida ou com outra pessoa exige saída manual.

A saída recebida é aplicada ao estado antes do join. Se o destino fechar/lotar nesse intervalo, o app mostra a saída real e a falha. São dois comandos existentes, sem transação atômica nem rollback simulado. Convite e código usam o mesmo caminho e conferem sala e membro realmente recebidos.

Mantidos: `StationLaunchPolicy`, retry explícito do R34, logs de abertura/escuta, Voltar Android e manifesto R34; design/faixa R33, motores, runtime online, relay, TLS/pin, licença/Keystore, saves, catálogo e downloads. Pronto confirma a sala atual; Start continua exigindo dois membros distintos/prontos e autoridade do servidor.

## Compilação e verificações

- **147** fontes Java/dependências; Java8, SDK36, DEX mínimo26; compilação concluída. Somente nota de APIs Android depreciadas herdadas.
- **20** verificações novas de entrada, duas salas individuais, dados ausentes, sala cheia/em preparação e membro já compartilhado.
- **107** regressões R34 do lobby: códigoTS1, convites, presença, bloqueios de ações e ordenação de snapshots.
- **60** regressões R34 de prontidão, feedback e caminho real do jogo.
- **15** regressões R34 da política de abertura host/guest e geração da sala.
- **11** verificações isoladas na lógica atual do servidor: duas salas → Pronto não entra → saída → join → dois membros → ambos prontos → start → host-listening → connecting → encerramento.

**213 verificações passaram.** Esses testes cobrem lógica e compilação; renderer Android, Binder/JNI e gameplay real entre dois aparelhos ainda requerem prova.

Módulo final compilado:

```text
/mnt/DADOS/station-second-player-check-20261005/build-r34/dex/classes.dex
SHA256 39864bd1eeabc2502f18dc252750eefc345d3e29f02c57e2838a9b004c18e1a4
336988 bytes
```

Pacote privado para a montagem no Windows: `/mnt/DADOS/station-second-player-check-20261005/station-second-player-r34-20261005.zip`, com `classes35.dex`, três fontes, receita e evidências. SHA integral em arquivo `.zip.sha256` ao lado. Não contém APK assinado, chaves, credenciais, licença ou jogos.

## Montagem e instalação no ambiente do aplicativo

**Substituir somente classes35.dex no APK R34 exato:**

```text
G:\BAKUP SISTEMA APP 03-10-2026\apks-candidatos-visuais\TurboStations-Salas-Retorno-R34-20261005.apk
SHA256 513dd4700192b994d93cdaf6cd55b79eccb804fa33eda43166304f5d2bcdb5ef
2053907144 bytes
```

1. Atualizar o clone pelo ramo ativo `feat/station-capas-visuais-netplay-20261003` ou pelo ramo da correção e ler este handoff. Se uma sucessora já existe, conciliá-la antes de empacotar.
2. Para reconstruir o DEX, executar a receita no clone com os snapshots completos: `python versions/station-second-player-20261005/recipes/build_second_player.py --android-jar CAMINHO --client-jar CAMINHO --r8-jar CAMINHO --output DIRETORIO_NOVO`. Caminhos opcionais de java/javac/jar; cliente compatível R10/R16. A receita restaura R27+R30+R34, confere hashes do R34 e aplica as três fontes. Builds com outro SDK/JDK devem registrar seus próprios hashes.
3. Para usar o DEX já compilado, conferir seu SHA256 acima e o resultado de build. Adaptar uma cópia da receita de pacote R34: base R34, saída nova, única substituição `classes35.dex`. **Preservar AndroidManifest.xml do R34**, em vez de repetir a montagem R34 sobre R33.
4. Comparar todas as entradas com R34: mesmo conjunto sem assinatura, só classes35.dex diferente; **13.086 demais entradas devem permanecer iguais** (R34 tinha13.085 preservadas e2 mudanças sobre R33). Conferir especialmente manifesto, recursos, carouselSO R33 e runtime online22ee3f67. Alinhar16KiB, assinar e verificar certificado original `7b16ee1aca7db7a50e7cc6c8612cf2a3568f474894a468865d842bf720c89825`.
5. Fora de partida/download ativo, atualizar ambos com `adb install -r --no-incremental`, preservando dados. Conferir hash integral em cada aparelho, versão, sessão e recibos. Não desinstalar ou limpar dados.

**Este delta ainda não foi montado em APK assinado nem instalado.** R34 continua a última instalação confirmada no Samsung; a versão do POCO precisa ser lida. O Windows disponibilizado pelo conector estava offline, e este Linux não possui o APK base/assinatura/aparelhos ADB. A fonte e o DEX final estão prontos para a montagem.

## Servidor e contrato

Produção mantida em **`e4e557a985ac5bead24885149c8650a5bb2dfae8`**, PID660598, DLL `7ecb6c8d94c5ab42c26638b5f9bdf7ffd0e70f99e047463ee5293af34f7bdcf5`. Nesta rodada houve documentação e teste isolado, sem restart, configuração, migration, chave ou licença modificada. Catálogo14/2212 e limites512salas/1024conexões preservados. O delta usa rotas existentes de `https://app.lzgames.com.br/v1/station/online`; nenhuma API nova é necessária.

| Etapa | Leitura/envio do app |
| --- | --- |
| Lobby | `enter`, heartbeat e `/online/events`; snapshot verificado e `selfId` próprio. |
| Destino | `snapshot.rooms[*].roomId/itemId/hostId/state/players/maximumPlayers`, ou convite/códigoTS1 da instância atual. |
| Sala própria | `snapshot.room`, membros e host reais. Pronto nessa sala não chama join em outra. |
| Saída | `leave` com requestId novo; aplicar resposta verdadeira antes de continuar. |
| Entrada | `join`, roomId e descritor preparado: itemId/engineId/contentSha256/optionsSha256/coreSha256/runtimeSha256. Conferir edição, não apenas título. |
| Comprovar entrada | `snapshot.room.roomId` igual ao destino e `members` contendo `selfId`; anfitrião também vê dois membros. |
| Início | `ready` de ambos → host `start` relay-wss-v1; `host-listening` somente após callback real. |
| Túnel | Cada participante pede seu ticket e abre WSS `/v1/station/online/relay`, Authorization StationRelay e station-relay.v1. |

Cada comando exige UUID-D/requestId novo e verificação de assinatura/domínio/licença/aparelho/sessão. Manter códigos de licença, Bearer, senha de sala e tickets privados.

## Próxima prova nos aparelhos

1. Duas salas distintas do mesmo jogo: contagem1/2, perfil com Entrar e outras salas com vaga.
2. POCO **Sair e entrar** → mesmo roomId com dois membros em ambos → ambos Pronto → anfitrião Iniciar.
3. Jogo ausente conserva a sala anterior e orienta baixar. Destino encerrado/cheio mostra falha real; código antigo após restart é recusado.
4. Conferir dois WSS com tráfego bidirecional, controles, sincronismo, áudio, saída, saves e retorno. Testar redes distintas.
5. Se ambos estiverem na mesma sala mas o convidado não abrir o jogo, capturar fases seguras do R34 (`StationRooms`, `StationGameSession`, `StationRetroActivity`), callback real host-listening e segundo túnel. A falha nativa ainda exige diagnóstico no aparelho.

Latência pública alta medida na entrega anterior continua pendente. Capacidade isolada não comprova partidas responsivas para centenas nem gameplay real em dupla. Downloads11be7f3/6f012a7 e ponte/BIOSCD seguem seus handoffs próprios.

# Segundo jogador — entrada na mesma sala — 05/10/2026

## Uso imediato no APK R30 instalado

**Estou pronto confirma a participação na sala atual. Não entra na sala de outro jogador.** Se cada telefone criou uma sala, ambos podem marcar Pronto e continuar sozinhos.

1. No POCO, tocar **Sair da sala**, sem sair da conta.
2. No primeiro telefone, manter a sala aberta e usar **Código da sala → Copiar código**.
3. No POCO, abrir **Código** no cabeçalho do lobby, colar o código TS1 e tocar **Entrar na sala**. O código da sala é diferente do código de ativação da licença.
4. Conferir os **dois nomes na mesma sala**. A mesma edição do jogo deve estar baixada nos dois telefones.
5. Ambos tocam **Estou pronto**; só o primeiro, que criou a sala, usa **Iniciar partida**. O convidado abre o jogo automaticamente quando o anfitrião confirma que o motor está escutando.

Não criar outra sala no POCO para participar da primeira. Se o jogo ainda não está instalado, voltar ao catálogo, baixar a edição indicada e repetir a entrada.

## Relato e evidência

O mantenedor informou: primeiro telefone mostra apenas um participante; no segundo a sala aparece sem Entrar; tocar Estou pronto não o reúne ao primeiro.

**Demonstrado no código R30:**

- O perfil de um anfitrião mostra “Em uma sala” e desabilita Convidar, mas não oferece Entrar.
- Quando há sala própria, a tela mostra somente essa sala e oculta a lista das outras salas públicas. Dois anfitriões sozinhos não conseguem escolher ali a sala um do outro.
- `ready` só atualiza a confirmação na sala atual; `join` exige primeiro deixar qualquer outra sala.

Duas salas individuais são consistentes com o relato. A lógica real do servidor reproduziu esse cenário: dois `create` geram IDs distintos; Pronto mantém um participante; `leave` seguido de `join` reúne os dois. **Não foi capturado snapshot autenticado nem screenshot desses dois Android neste Linux**, portanto a identidade exata das salas observadas não foi provada aqui.

Registros públicos entre22h35–22h52UTC não registram recusas de `/online/command`; dois upgrades101 ocorreram separados, durando8s/60s, sem aumento do contador de bytes do relay. Status/tamanho de corpo não identificam a ação nem o telefone; não inferir sucesso de gameplay dessas respostas. Houve409/499 em eventos:499 indica request cancelado,409 pode acompanhar saída/expiração; sem corpo/código não atribuímos causa específica.

## Correção do aplicativo entregue

Fonte: **`b282b04cf0cad81dc720ed4a41bfa70c9bcad63a`**, diretório `versions/station-second-player-20261005` no TurboElden. Base completa R30 restaurada e conferida pelo SOURCE-MANIFEST; **143 de146 entradas Java/dependências preservadas exatamente**. Apenas:

| Arquivo | Mudança |
| --- | --- |
| `StationPlayerModel.java` | Publica ação Entrar usando `roomId/itemId` reais; distingue sala própria individual, já compartilhada, cheia e em partida. |
| `StationPlayerSheet.java` | Perfil do anfitrião oferece **Entrar na sala** com habilitação pelo snapshot assinado. |
| `StationRoomsActivity.java` | Mostra **Outras salas com vaga** mesmo com sala própria individual, contagem1/2 e orientação sobre Pronto; confirma **Sair e entrar**, prepara o jogo antes de sair e usa `leave/join` existentes. |

Se o jogo local faltar ou o motor estiver incompatível, a preparação falha antes de deixar a sala própria. A troca só é oferecida para uma sala própria aguardando, com o usuário sozinho. Não transfere automaticamente uma sala em partida ou com outro participante. A confirmação avisa que a sala individual será encerrada.

A saída recebida é aplicada ao estado antes do join. Se a sala de destino fechar/lotar nesse intervalo, o app conserva a saída real e mostra a falha; **não simula rollback ou sucesso**. A sequência tem dois comandos, não é transação atômica. Convite e código também usam o mesmo caminho de entrada e verificam a sala/membro realmente recebidos.

Motores, libstation_retroarch, callback de escuta, relay, TLS/pin, licença/Keystore, saves, catálogo, download e carrossel R30 não foram alterados. O botão Pronto continua confirmando a sala atual; Start mantém dois membros distintos/prontos e autoridade do servidor.

## Compilado e testado

- 146 fontes Java/dependências; Java8, SDK36, DEX mínimo26; sem erro de compilação. OpenJDK21 apresentou três avisos de opção Java8 e nota de APIs Android depreciadas existentes.
- **20** verificações novas do modelo de entrada, duas salas individuais, dados ausentes, destino cheio/em preparação e membro já compartilhado.
- **107** regressões executáveis R27 de códigoTS1, convites, presença, bloqueios de ações e ordenação de snapshots.
- **11** verificações na lógica atual do servidor: duas salas próprias → Pronto não entra → saída → join real → dois membros → doisProntos → start → host-listening → connecting → encerramento.
- Total138 verificações relevantes. Não são renderer Android, Binder/JNI em aparelho ou partida real de dois jogadores.

Módulo privado compilado:

```text
/mnt/DADOS/station-second-player-check-20261005/build/dex/classes.dex
SHA256 ef8a2d99202c438cf867bd7907aba06f67a3c08a3256bc44599d32a925866ed5
332492 bytes
```

Substituir somente **classes35.dex** no APK base exato R30:

```text
G:\BAKUP SISTEMA APP 03-10-2026\apks-candidatos-visuais\TurboStations-Voltar-Salas-R30-20261005.apk
SHA256 1768b7df440dcdf99efbd2e8ff93181eae06edaab5bb9be85e073afeb639027b
```

A receita `versions/station-second-player-20261005/recipes/build_second_player.py` restaura R27+R30 por hashes, aplica somente as três fontes e compila o módulo em diretório novo. Informar `--android-jar`, `--client-jar`, `--r8-jar` (jar que contém D8), `--output` e caminhos de java/javac/jar se necessário. Usar cliente compatível do buildR10/R16; nenhum método novo do cliente foi exigido. Os SDKs/JDKs do Windows devem produzir e registrar seus próprios hashes de build.

Na montagem Windows: partir de R30 ou conciliar primeiro qualquer sucessora recebida; preservar13.083 outras entradas e todos os payloads nativos; alinhar16KiB e assinar com certificado original `7b16ee1aca7db7a50e7cc6c8612cf2a3568f474894a468865d842bf720c89825`. Atualizar ambos os telefones com `adb install -r --no-incremental`, sem desinstalar/limpar dados. Conferir hash integral no aparelho e registrar recibo. Nunca reenviar R12/R27 por causa deste delta.

**Ainda não há APK assinado/instalado desta correção.** O R30 permanece a última instalação comprovada. O Windows disponibilizado pelo conector estava offline; este Linux não dispõe do APK base/certificado privado/aparelhos ADB. A fonte e o DEX estão prontos para a montagem no ambiente do app.

## Servidor e contrato preservados

Produção continua **`e4e557a985ac5bead24885149c8650a5bb2dfae8`**, PID660598, DLL `7ecb6c8d94c5ab42c26638b5f9bdf7ffd0e70f99e047463ee5293af34f7bdcf5`. Apenas documentos/teste isolado nesta rodada; **zero restart, configuração, migration, chave ou licença modificada**. Catálogo14/2212 e512salas/1024conexões configuradas preservados. A correção do app usa rotas existentes; não exige API nova.

| Etapa | Leitura/envio do app |
| --- | --- |
| Lobby | `enter`, heartbeat e `/online/events`; usar snapshot assinado e `selfId` próprio. |
| Sala de destino | `snapshot.rooms[*].roomId/itemId/hostId/state/players/maximumPlayers`, ou invite/códigoTS1 da instância atual. |
| Sala própria | `snapshot.room`, membros e host reais. Pronto nesta sala não executa join em outra. |
| Saída | `leave` com requestId novo; renderizar resposta verdadeira antes de continuar. |
| Entrada | `join`, roomId e descritor preparado: itemId/engineId/contentSha256/optionsSha256/coreSha256/runtimeSha256. Não casar só por título. |
| Comprovar entrada | `snapshot.room.roomId` igual ao destino e `members` contendo `selfId`; anfitrião também passa a ver dois membros. |
| Início | `ready` de ambos → host `start` relay-wss-v1; anfitrião só envia `host-listening` após callback real. |
| Túnel | Cada participante obtém seu ticket e abre seu próprio WSS `/v1/station/online/relay`, Authorization StationRelay e station-relay.v1. |

Sempre UUID-D/requestId novo e verificação de assinatura/domínio/licença/aparelho/sessão. Não publicar código de licença, Bearer, senha de sala ou ticket. IDs de sala/TS1 não são credenciais de ativação.

## Próxima prova no aparelho

1. Duas salas distintas do mesmo jogo: conferir contagem1/2, perfil com Entrar e outras salas com vaga.
2. POCO **Sair e entrar** → dois membros no mesmo roomId em ambos; Pronto em cada; só anfitrião inicia.
3. Jogo ausente no POCO deve orientar baixar e conservar sua sala anterior. Destino encerrado/cheio deve informar falha real; código velho após restart deve ser recusado.
4. Conferir dois WSS e tráfego bidirecional, ações dos controles, sincronismo, áudio, saída, saves e retorno. Testar Wi-Fi+móvel e redes distintas.
5. Se ambos constarem na mesma sala e o convidado ainda não abrir o jogo, registrar o erro/logcat seguro do R30/sucessora e conferir callback real **host-listening** e abertura do segundo túnel. Esta correção de entrada não afirma resolver uma falha nativa não comprovada.

Latência pública alta medida na entrega anterior continua aberta. Não promover centenas de partidas responsivas nem gameplay real em dupla com base nos testes desta entrega. Delta downloads11be7f3/6f012a7 e ponte/BIOSCD continuam separados.

# Conexão real do anfitrião — candidato sobre R41 — 06/10/2026

## Estado

Delta de três fontes implementado e testado em Linux, sem modificar o snapshot R41. **Não há APK novo compilado ou instalado nesta entrega.** O computador Linux não tem o Android conectado por USB, o APK R41 integral nem a chave de assinatura Windows. A produção do APK recebeu primeiro o diagnóstico no commit `d71b542` e o usuário informou que conectará o telefone que abre o jogo ao PC de produção.

A tentativa real de Battletoads tinha dois membros Pronto e start/ticket200; a sala continuou starting, sem host-listening observado e sem novos bytes de jogo. Isso localiza o bloqueio na prontidão do anfitrião. **A causa específica no motor/JNI/Binder ainda não foi capturada.** Não afirmar que o callbackJNI necessariamente falhou nem que a partida real já foi corrigida.

## Mudança

- `StationHostConnector`: conecta somente ao TCPloopback do motor e conserva a primeira conexão bem-sucedida como fluxo do jogo. Não abre uma conexão descartável de teste. Aguarda o motor por até45s, com tentativas de50ms, cancelamento e fechamento dos sockets que falharam. O avisoJNI pode acelerar uma tentativa, mas sozinho não confirma prontidão.
- `StationRelayTunnel`: inicia WSS, conecta o TCP real e só chama ready quando ambos estão abertos. Preserva protocolo/pin/tickets, quadros32KiB, encaminhamento16KiB e controle de fila256KiB. Classifica falha do listener nativo, relay, fluxo local ou protocolo sem registrar segredos.
- `StationRetroActivity`: passa host-listening pelo Binder existente após prontidão real do túnel. Mantém o callback nativo como aviso; no transporte direto mantém o contrato anterior. Ignora prontidão atrasada após fechamento, conserva aviso atéonStart e mostra erro específico se o motor não abrir. O convidado continua aguardando connecting assinado.

Não muda runtime/core/opções/hash de jogo, motores offline, saves, autenticação, sessões, catálogo, downloads ou servidor. O listener real e a sessão autenticada continuam necessários; nenhum estado é forçado para fazer a tela avançar.

## Testes e limites

39 verificações isoladas:10com TCP real (porta que abre depois, semJNI, socketretido/dados nos dois sentidos, falso aviso, deadline/cancelamento);9do túnelJava real porWSS com TLSfixado e12.583.029bytes em cada direção;20do relayC# atual, estado starting→connecting após prontidão, geração/tickets/revogação/limpeza. A versão congeladaR41 falha no mesmo transporte semcallback (`Accept timed out`); o candidato passa. Esses endpoints TCP representam o motor; **não são duas emulações Android**.

150fontes Java compilaram com Java8 e o SDK Android34 exato daR41 (`6cea1df3...`). A compilaçãoLinux é **api-check-only**, com `station-client.jar` derivadoR16 (`c643277c...`), para conferir o contrato de tipos; não gera DEX/APK publicável. O buildWindows exige o jar de produçãoR41 `e4185497...`, D8 `d43c8a94...` e SDK exatos antes de gerar DEXmin26. Recibos e logs em evidence; fonte/receitas em SOURCE-MANIFEST.json. Gameplay físico, qualidade de imagem/controle, versão do segundo telefone e latência externa continuam pendentes.

## Execução na produção Windows

1. Ler `docs/server/RETORNO-BATTLETOADS-CONEXAO-HOST-STATION-20261006.md`. **Capturar Activity/logs do telefone que abre o jogo antes de atualizar** e resumir fases/códigos sem publicar logs com dados pessoais/segredos. O caso poderá exigir ajuste adicional do motor ou Binder; este delta corrige a dependência exclusiva do avisoJNI.
2. Atualizar a branch `feat/station-capas-visuais-netplay-20261003` ou `fix/station-relay-ready-return-20261006`; usar esta pasta como fonte. Os scripts operam a partir do Git e criam um workspace novoE. Não sobrepor a fonteEcongeladaR41.
3. Compilar com os arquivos de produção já registrados pelaR41:

```powershell
python versions/station-relay-readiness-20261006/recipes/build_candidate.py
python versions/station-relay-readiness-20261006/recipes/package_candidate.py
```

Workspace: `E:\ESTUDO APK\work\station-relay-readiness-20261006`. Build exige JDK17/SDK34/station-client.jar/D8exatos conformeR41; não usar `--api-check-only` para montar APK. O script recusa workspace/artefato anterior. Se precisar repetir, usar diretório novo e passar o mesmo `--workspace` ao packager. Recibo em `build/final/result.json`.

Baseexata: `G:\BAKUP SISTEMA APP 03-10-2026\apks-candidatos-visuais\TurboStations-Comunidade-R41-20261006.apk`, SHA`b6b19321ec529945870dc919c3de5f0eba75aa54330d3672227b8e632302b28d`. Packager troca somente `classes35.dex`, confere todas entradas, alinhamento16KiB e certificado original `7b16ee1aca7db7a50e7cc6c8612cf2a3568f474894a468865d842bf720c89825`. APKnovo: `TurboStations-Conexao-Host-Candidato-20261006.apk`, na mesma pastaG. O packager deriva da receitaR41 e ainda não foi executado nesta máquina por ausência dos artefatos Windows.

4. Quando não houver partida em andamento, atualizar os dois telefones com a mesma assinatura, preservando dados/licença/saves. Conferir SHA integral instalado e registrar recibo sem serial/código secreto emGit. Não desinstalar/limpar dados nem instalar versão antiga.
5. Battletoads nos dois: mesmaedição→mesmasala→2nomes→ambosPronto→hostIniciar. Fasesesperadas: local-stream-ready(host)→host-listening-ack→connecting no convidado→Activitycliente→doisWSS→dados/gameplay. Se local-stream-ready não vier, capturar erro nativo; se vier semack, diagnosticar Binder/session-sync. Se ackvier mas cliente não abrir, capturar launch stages/erro no convidado. Testar inputs dos dois e Voltar em cada aparelho. Publicar recibo com limites reais; só então considerar a partida validada.

## Repetir testes isolados

Em Linux/Windows, com Java e projeto atual do servidor e diretório novo:

```sh
python versions/station-relay-readiness-20261006/recipes/run_tests.py --output /caminho/novo --server-project /caminho/Servidor-pix/src/TurboRamaSuiteOnlineServer/TurboRamaSuiteOnlineServer.csproj
```

Podem ser passados `--java`, `--javac` e `--dotnet`. Certificado/tickets/identidades são sintéticos, endpointloopback; nenhum banco/cliente de produção é tocado. Não copiar binários, APK, BIOS, ROM ou keystore paraGit.

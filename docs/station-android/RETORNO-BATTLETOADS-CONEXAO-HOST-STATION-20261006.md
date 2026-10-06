# Battletoads: sala aceita, conexão do anfitrião pendente — 06/10/2026

## Evidência nova de produção

A tentativa observada às18h02min53s de Maceió (`2026-10-06T21:02:53Z`) é **SNES / Battletoads in Battlemaniacs (USA)**. Os dois membros estavam na mesma sala, ambos Pronto. `start` retornou200, geração3, transporte `relay-wss-v1`; o anfitrião recebeu `relay-ticket`200. Uma conexão WSS foi aberta e terminou após60.001,8673ms. Não foi observado `host-listening`; não houve incremento nos bytes encaminhados pelo relay durante a tentativa. O convidado recebeu estado `starting` e continuou no lobby.

Isso localiza a falha entre abertura do motor Android e confirmação de prontidão do anfitrião. Não comprova se o socket nativo não abriu, se o callback JNI não chegou ou se o Binder/processo principal falhou. O servidor não recusou o segundo membro nem o início. **Gameplay em dupla continua sem validação.**

API permanece `a2bb176530fd4d2dfa740da7e934fd84d097404e`, DLL `d181bf97d5b39a334e95144267d6ece3f11d4e659a314d7d16cd2746e1999e13`, PID875574. Não forçar `connecting`, não abrir convidado prematuramente, não mudar autenticação nem allowlist por inferência.

## Captura solicitada ao operador do APK

O usuário informou Samsung da loja e confirmou que conectará por USB **o telefone que abre o jogo** ao PC de produção. Não atribuir os papéis ao modelo ou à licença por suposição. Esta máquina Linux não tem esse Android conectado por ADB.

1. Conferir `adb devices -l` e escolher explicitamente o serial. Registrar modelo, versãoAndroid, pacote instalado e versão do APK. O recibo anterior R41 comprova apenas Samsung SM_A566E/hash`b6b19321ec529945870dc919c3de5f0eba75aa54330d3672227b8e632302b28d` naquela instalação; não prova a versão dos dois telefones agora.
2. **Capturar antes de instalar ou encerrar o jogo**: Activity em primeiro plano e logs `StationRooms`, `RetroArch`, `AndroidRuntime`, no diretório privado `device-evidence`, fora do Git. Não limpar dados/logs, regenerar códigos ou abrir outra sessão pelo processo nativo.
3. Reproduzir uma vez com dois membros Pronto e anfitrião Iniciar. Procurar as fases `launch stage=activity`, `game stage=native-listening`, `game stage=host-listening-ack`, `session-sync`, erros `[Netplay]` e morte do processo principal. Conferir se a Activity é `StationRetroActivity` no processo `:station_netplay`, biblioteca `station_retroarch`, papelhost e porta55435. Os extras/config podem conter segredos: não publicar dumps integrais.
4. Se `native-listening` está ausente, verificar o erro nativo e se o runtime carregado tem os patches de papelhost/client e listener. Se o sinal chega, investigar entrega por ResultReceiver/Binder e a chamada autenticada `host-listening`. Registrar apenas fase/classe/código e resultados, sem sala, apelido, ticket, bearer, senha ou código de acesso.
5. Preservar licença, saves, assinatura, todos motores e fonte congeladaR41. Atualizações são APKsucessor sobre R41, sem desinstalar/limpar. Não aplicar R34 antigo sobre R41.

## Delta implementado e testado; instalação pendente

Fonte: `versions/station-relay-readiness-20261006`, três classesJava sobreR41, sem alteração do snapshot/runtime congelado. O túnel mantém a primeira conexãoTCP real e anuncia prontidão após TCP conectado/WSS aberto; o avisoJNI acelera a espera. O convidado continua aguardando confirmaçãoautenticada. Falhas do motor/relay ficam explícitas, incluindo as ocorridas antes doonStart.

39verificações TCP/TLS/relay passaram,12.583.029bytes por direção; baselineR41 falha no mesmo transporte semcallback.150fontes compilaram Java8/API34, usando SDKexato e classpathAPIderivadoR16. Não há DEX/APKnovo produzido nesta máquina. A receitaWindows exige jar/D8deproduçãoexatos e preserva todas entradas exceto classes35.dex sobre o APKR41/hashb6b19321.

Ler README/STATUS/evidence e executar build_candidate.py→package_candidate.py no PCWindows apóscapturaprivada doAndroid. Atualizar com assinaturaoriginal; preservar licença/jogos/saves. Ainda é necessário verificar o motor/JNI/Binder nos aparelhos e confirmar inputs/gameplay dos dois. O delta não comprova que o JNI foi a causa específica da tentativa real e não autoriza declarar gameplayresolvido.

## Cópia de códigos de acesso

A melhoria de compatibilidade do botão Copiar código já está publicada no site, fonte247bb0a/retornoservidor0820fd0,17h59Maceió. Ler RETORNO-COPIA-CODIGOS-STATION-20261006.md e COPIA-CODIGOS-STATION-PRODUCAO-20261006.json. O usuário havia conseguido ativar antes dessa publicação; não reemitir códigos para diagnosticar a sala. API/admin/helper/licenças/servidorR41 preservados.

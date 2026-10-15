# R165: cadastro aplicado e saída confirmada do anfitrião publicada

Estado efetivo em 2026-10-15T18:40:08.489375+00:00. Este retorno conclui o pedido
`2d4240f` e a parte de servidor do conserto `220bc64`; os documentos originais
de pedido continuam congelados como histórico. Ler o topo do **mesmo handoff**
e [recibo efetivo](server-return.json).

## Cadastro e modos

Kit privado `2e260ba8`, 12.985.386 bytes: 43 arquivos/42 hashes e 27 ELF reais
conferidos. Nenhum APK foi solicitado/recebido neste Linux. Instalação A56,
APK `9e7ab244` e assinatura são prova do produtor; não foram inspecionados aqui.
Core e 21 dependências são os mesmos da R164. Runtimes reconstruídos têm novos
hashes; não se afirma igualdade byte a byte de suas seções de código. Fontes,
fingerprints e políticas imutáveis foram comparados ao material congelado.

Adicionados quatro motores exatos e 492 perfis/modos, com baseline protegida
fresca, autenticação Linux, scan.lock, backup e recomputação pelo registrador
vigente. Anteriores preservados. Snapshot: catálogo100,
83 motores/15912 perfis/10997 modos.
JSON compacto permaneceu abaixo de 8 MiB; nenhum limite foi relaxado.

- PSX: todas as 84 edições reavaliadas; 93 decisões, 23 modos de duas portas
  em 19 edições aprovados. Dezessete modos continuam impedidos; 53 classificações
  locais concedem zero vagas. Evidência de core da revisão anterior foi reutilizada
  somente após conferir conteúdo/core/opções/controles/fontes do novo owner;
  não é nova execução do APK. [Decisões](psx-reviewed-decisions.json),
  [CSV atual](psx-editions-modes-r165.csv) e [mapa/discos original](../psx-online-20261015/README.md).
- N64: 157 edições/387 perfis; 120 edições multiplayer e 37 somente individuais,
  230 modos multiplayer e 157 modos individuais sem vagas. CPU Dynarec,
  Glide64mk2/inputDelay2 mantidos. Não confundir número de perfis com edições.
- GameCube: nove modos; Wii: três. Somente modos anteriormente comprovados e
  requalificados pela identidade completa. [492 vínculos atuais](profiles-r165.json).

## Saída do anfitrião e serviço

Código `c0ca229802909556223e54b3f610f084081b2837`, DLL `53d4adc9c1143f09c6b136ee085cb599dbe58a286b582107cc88a640483b2eb2`. Passaram 1.467 verificações
unitárias, 226 HTTP/TLS e sete regressões dos dados. Sombra com o mesmo sandbox
efetivo passou antes de tocar a API. Houve 2 reinícios
controlados somente do Station, sem sala/conexão/recuperação em curso. PID final
152812. Drop-ins antigos, bind mounts,
releases e possibilidade de retorno foram preservados.

Somente `leave` confirmado reserva até dois segundos para enviar TSR3 `STATE=3`
antes do fechamento normal 1000. Frame de jogo em trânsito não cancela esse
envio. Depois de CloseOutput, o leitor termina a troca de Close dentro do
mesmo prazo original; evita abortar imediatamente com dados em trânsito.
Perda de rede sem confirmação continua recuperável. Fonte e testes em
`StationMultiplayer.cs`, `StationMultiplayerStream.cs` e seus dois projetos de testes.
A prova pública recebeu estado terminal antes do fechamento com tráfego
concorrente em três salas independentes, repetindo o mesmo leave/requestId
três vezes por sala com resposta idempotente. Os dois sockets continuam lendo
independentemente da chamada HTTP de saída.
O teste determinístico C# cobre a corrida exata; a prova WAN não afirma seu timing.

A primeira DLL5722 passou nas consultas e transporte, mas sua prova pública
de saída falhou. A regressão nova demonstrou Abort antes do Close recíproco;
o conserto final tem teste negativo antes/positivo depois. A
[RFC6455](https://www.rfc-editor.org/rfc/rfc6455#section-1.4) descreve o risco de
perder dados nesse encerramento. Não houve captura TCP para atribuir aquele
episódio especificamente a RST ou ao Cloudflare.

## Consumo e aceite físico

As consultas capabilities assinadas passaram nas 84 edições PSX e 157 N64,
localmente e pelo domínio público, usando os IDs R165. Passaram admissões e
recusas, transporte público PSX2P/estado real de 4.456.448 bytes e N64 de quatro
clientes sintéticos, READY, pausa/retomada, TLS/SPKI e prioridade. As identidades
de payload sintéticas não executam os protocolos nativos nem gameplay Android.
Fixtures próprias removidas; licenças reais, esquema e serviços compartilhados
preservados. [Prova final](evidence/public-qualification.json).

As provas integrais de edições e payload ocorreram na DLL5722; o ajuste final
mudou somente a finalização após leave confirmado. Elas foram conservadas,
sem refazer as 241 edições. Na DLL final, passaram novas consultas dos quatro
motores/admissões GC/Wii e as três provas públicas de fechamento, além das
1.467+226 verificações. A proveniência dos dois binários consta no recibo.

O app deve usar capabilities filtradas por itemId/conteúdo/engine/core/runtime/
profileId/profileSha256 do **motor instalado**. `clientMaximumPlayers=5` é a
descoberta genérica atual; não cria vagas extras (duas pessoas no PSX, até quatro no N64). `approved=true`
com contagens[]/máximo1 continua sem sala. R164 segue cadastrada; não selecionar
seus perfis na R165 por disponibilidade agregada. Consultar catálogo?metadata=1;
capas permanecem coverId + revisão do item.

Para teste físico, os dois celulares precisam da mesma R165, edição e modo
aprovado. Confirmar primeiro quadro, som, controles independentes e, após sair o
anfitrião, aviso/retorno do convidado por pelo menos 25 segundos. A56 tem recibo
piloto; g52 na R165 não está comprovado neste retorno. Não promover gameplay
R165 nem quatro Android por causa de testes sintéticos. R164 estável preservada.

Autoridade privada `/mnt/DADOS/station-psx-return-20261015-1736/completed.json`, PASSED. **Não repetir o cadastro,
os publicadores PSX/R164/capas nem esta implantação.** Retorno de serviço remove
somente o novo drop-in próprio e reativa a DLL b347 preservada; não desfaz os
dados aditivos ou bindings antigos. Nenhuma mudança de Wi-Fi/onboard/DNS,
banda100/800, ROM, arte, save, chave/licença real ou outro produto.

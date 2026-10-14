# R149 — reparo mínimo do Dolphin offline, APK instalado e kit na mídia de repasse

Fonte/receita congelada em `ef5d00b189a4a57bb2044e297b2f1b9b4fa90ce0`.
O APK R149 foi compilado e assinado; APK e kit foram relidos diretamente no
backup local e coincidem com os recibos de produção. A instalação USB no
Samsung A56 concluiu com `SUCCESS` em 14/10/2026 às 13:16:35 UTC. O SHA-256
do APK instalado coincide com o da R149 e o procedimento não desinstalou,
limpou dados nem forçou a parada do aplicativo. O kit foi copiado à mídia de
repasse com hash conferido anteriormente, mas **não há recibo de aplicação no
servidor**. A R143 continua a referência estável.

No Samsung A56 com R148, *The Legend of Zelda: The Wind Waker* falhou antes
do jogo abrir com `LinkageError`: `EmulationActivity.onPause()` tenta
sobrescrever `FragmentActivity.onPause()` declarado `final` e ainda reduz o
acesso de `public` para `protected`. O diagnóstico leu diretamente APKs R126,
R128, R134, R143 e R148; a colisão existe pelo menos desde R126. Trata-se de
falha **offline**, anterior à sala e independente do cadastro do servidor.
Fonte: `TurboElden/docs/server/r148/GAMECUBE-OFFLINE-DIAGNOSTICO-20261014.md`.

A receita R149 muda somente dois sinalizadores em `classes10.dex` da R148:
`FragmentActivity.onPause()V` de `public final` (`0x11`) para `public`
(`0x01`), e `EmulationActivity.onPause()V` de `protected` (`0x04`) para
`public` (`0x01`). Mantém corpos, bytecode, tabelas, offsets e tamanho;
recalcula o cabeçalho DEX e prova a inversão byte a byte. A receita de
montagem prevê cinco entradas renovadas: DEX10, três marcadores nativos de
identidade e `engines.json`; as outras 13.855 entradas do APK R148, inclusive
core Dolphin, carrossel, downloads e transporte, devem permanecer exatas.
O recibo final confirmou as cinco substituições/13.855 entradas preservadas,
assinatura original v2/v3 e alinhamento 16 KiB. A versão visual preparada
separadamente não faz parte deste reparo funcional. Fonte:
`TurboElden/integration/dolphin-onpause-r149/README.md`.

APK assinado R149: **2.715.089.935 bytes**, SHA-256
`698e8a7914cc7be2facc00e6701ff1f3533fa13f763358b51578a35d25c2741e`.
Kit privado `Kit-Servidor-TurboStations-R149-Independentes.zip`:
**12.181.272 bytes**, SHA-256
`ed95ba61c92285937014a91e86b1dd7dd0a6928e10a9d063132973244e4eb5ec`.
O kit contém 38 arquivos (manifesto mais 37), seis ELF, vinte dependências
distintas e os mesmos 78 modos revisados. Ambos os hashes foram conferidos
diretamente nos arquivos privados, que não são publicados no Git. O recibo
de backup R149 concluiu com sucesso; cadastro no servidor continua `false`.

## Teste e dependência do servidor

O teste **offline** da R149 no A56 ultrapassou o antigo `LinkageError` e abriu
o Dolphin. Em seguida, a edição europeia cadastrada de *Wind Waker* falhou com
`The disc could not be read (at 0x55e3c040 - 0x55e3c060)`: a leitura começa
no byte 1.440.989.248, mas o ISO instalado mede 546.776.387 bytes. Seu SHA
coincide com o catálogo; o cabeçalho `GZLE01` discorda do nome europeu. O
diagnóstico e a quarentena **somente desse item** estão em
[ERRO-LEITURA-WIND-WAKER-20261014.md](ERRO-LEITURA-WIND-WAKER-20261014.md).
Após o aviso de leitura, o usuário saiu do jogo no A56 e informou que o
TurboStations voltou normalmente à lista de jogos, sem travar. Essa saída
após erro foi observada pelo usuário, não por registro USB. Pausa, retomada,
saída após uma partida jogável e gameplay completo continuam pendentes até
ter uma imagem verificada. A falha de mídia não exige recompilar o APK nem
cadastrar o kit online para ser investigada.

O **online** dos motores independentes exige o kit privado **R149 exato** e as
capacidades assinadas destes quatro IDs renovados:

| Plataforma | ID R149 |
| --- | --- |
| PSX | `pcsx-rearmed-ff81ed17-player-v3-29c3d7b9e6a8` |
| N64 | `mupen64plus-ae-33bf7021-n64-v3-dcad075758e5` |
| GameCube | `dolphin-5102a033-gamecube-v3-c80ec04d1723` |
| Wii | `dolphin-5102a033-wii-v3-c80ec04d1723` |

O kit R148 não foi aplicado e não cadastra identidades R149. O último cadastro
comprovado neste handoff continua R141. Antes de receber o kit, obter baseline
protegido fresco e verificar os seis ELF, vinte dependências e 78 modos
revisados (66 N64, nove GameCube, três Wii; zero novas aprovações PS1 HLE). Aplicar
aditivamente pelo preparador/registrador vigentes sob `scan.lock`, mantendo
IDs, perfis, modos, licenças e dados anteriores. Não reaplicar R141 ou QoS,
executar operadores históricos, implantar ou reiniciar a API para este kit.
Ver [instrução de recebimento](RECEBIMENTO.md).

**Pendente:** quarentena da imagem defeituosa no servidor, verificação da fonte
e nova edição jogável; depois confirmar leitura, gameplay, pausa, retomada e
saída após partida no A56; recebimento e cadastro aditivo do kit no servidor,
capacidades assinadas e sessões online reais. A instalação confirmada não
comprova que o jogo é jogável. Só após essas provas se pode avaliar
acesso online e aceite ao consumidor. O estado exato está em
[STATUS.json](STATUS.json) e no [recibo sanitizado de instalação](evidence/INSTALACAO-A56-SUCESSO-20261014.json).

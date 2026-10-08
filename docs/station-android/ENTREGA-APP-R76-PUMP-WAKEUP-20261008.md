# R76 — APP → SERVIDOR: APP-01 integrada e compilada

Fonte exata: `2a8adce752b7778c90b2e70ecd67d1bc1fc62a9d`, branch `fix/station-pump-wakeup-r76-20261008`.
[Fontes, receitas, evidências e estado](https://github.com/luziellacerda/TurboElden/tree/2a8adce752b7778c90b2e70ecd67d1bc1fc62a9d/versions/station-pump-wakeup-r76-20261008). Retorno do servidor analisado: `ed9ca9fdcc16f3b9f86792d43ed01d5812a9f1d8`.

Esta é a entrega do aplicativo ao operador. Não é retorno do servidor nem implantação Linux.
APK `d7145db3511a4056b16fdde10e4c07709a16b7f445596801b3535c1b28b06a51`; DEX35 `c03ea2f4aa30c5e32654c575115583f72815b9701c16791c4f94c6ade753f2ff`.
Runtime `804b2acfea4c6d615bf40dba30e1777015098bf7375d0745db8d117555eb2516` preservado, motores rs4 já ativos. Não cadastrar novos IDs nem reiniciar por esta entrega.
R75 visual incluída. Instalação individual: `['Samsung A56']`; conferir os recibos, sem tratar testes sintéticos como gameplay físico.

Ações do operador na seção 6: receber correção APP-01, preservar registro, resolver a divergência TLS da candidata 6f27 e correlacionar coleta da próxima sessão. Os testes R76 usam código servidor 32ce/ab192bf, NÃO homologam a DLL 6f27.

---

# R76 — APP → SERVIDOR: sinalização da fila corrigida

Este documento entrega código Android e evidências ao operador do Servidor-pix. Não é um retorno do servidor nem autorização para reiniciar serviços. O mantenedor pediu integrar a correção APP-01 após a leitura do retorno `ed9ca9fdcc16f3b9f86792d43ed01d5812a9f1d8`.

## 1. Base exata e identidade

- Aplicativo: branch `fix/station-pump-wakeup-r76-20261008`; commit exato consta no recibo de publicação e no cabeçalho da cópia enviada ao servidor.
- Código Java base: R74 `557014b4ff5ec5c3c0162847d922c0587f68b0e9`, composição de 201 fontes reconstruída e conferida integralmente.
- Pacote base: R75 visual `1ab4fa3770570832ea5ff2e9b0ce4f8a26e0e24e210ad7652f4c96647fecad32`. Corrige os cinco nomes de vídeos Neo Geo; preservado na R76.
- Manifesto do novo APK, tamanho, hash completo, certificado, comparação de todas as entradas e estado de instalação: `STATUS.json`, `evidence/package.json` e eventuais recibos `evidence/installation-*-r76.json`.
- Temporários/compilação: `E:\ESTUDO APK\work\station-pump-wakeup-r76-20261008`. APK final: `G:\BAKUP SISTEMA APP 03-10-2026\apks-candidatos-visuais\TurboStations-Premium-R76-20261008.apk`.
- Receitas e overlay ficam nesta pasta; não reconstruir a partir de uma Activity antiga ou de outra árvore não identificada.

## 2. Defeito e mudança de código

Arquivo alterado: `java/netplay-src/org/emulationstation/frontend/netplay/StationRecoveryTunnel.java`.

SHA anterior `8af5ceb585498512ff9ff4e1409a46ee2ed9dca9ee66bcb0c43bc11fa7817896`; SHA corrigido `442c50840a280d4a5605e32a06873fa14a879285a959a6f2a0b5231e2204ede9`.

Antes, o escritor podia observar a fila vazia; outro produtor acrescentava dados e chamava `pump()` antes de o escritor liberar `pumpPending`. O produtor encontrava a marca ocupada e retornava. O escritor terminava sem guardar aquele pedido. Os bytes continuavam presentes, mas dependiam de outro evento/tick para envio. O tick de 250 ms não mede o atraso real nem é um limite absoluto de latência.

Agora `pumpRequested` registra a solicitação antes de tentar obter `pumpPending`. O escritor consome a solicitação antes da próxima leitura de estado/fila. Ao terminar, libera a propriedade e verifica se uma solicitação chegou no intervalo. Se houver, agenda nova drenagem no mesmo executor. Mantém um único escritor; não cria envio paralelo, fila ilimitada, redução de timers ou repetição ocupada.

O caminho de erro captura o `Remote` realmente usado: uma falha tardia do transporte antigo chama `lost(antigo)`, cujo guard de identidade impede derrubar o substituto. A rejeição do executor libera a marca e conserva a solicitação, aguardando um próximo chamador; não recursa para um executor que rejeita. No executor real a rejeição ocorre após encerramento. `closed` impede reagendamento. Os testes cobrem rejeição concorrente, encerramento durante espera e substituição de transporte.

**Limite da mudança:** DEX35 alterado; DEX28, manifesto Android, carrossel R75, vídeos, controles, cores dos emuladores, runtime nativo e autenticação preservados. A prioridade ACK/PAUSE/DATA/READY/PING, crédito, buffers, offsets, replay e timeouts permanecem iguais. Não foi aplicado o experimento de atraso de entrada 2/0.

## 3. Cadastro já ativo: nenhuma nova engine necessária

Runtime preservado `804b2acfea4c6d615bf40dba30e1777015098bf7375d0745db8d117555eb2516`.

| Plataforma | ID ativo | Core SHA-256 |
|---|---|---|
| SNES | `bsnes-mercury-performance-79d7f9de-rs4-804b2acfea4c` | `cc14438319709f3b7868b3e652c731f6df472ff21bb01e4a65234ba2cc6f368b` |
| Mega Drive | `clownmdemu-d43c2708-rs4-804b2acfea4c` | `109b62ac11b2f59572de0666bcb02b3701676896b91cb32529bbe5c5032b6b69` |

Protocolo `station-stream.v2`; manifesto de engines `d43d6af1581691fbf88d6a15f87bea66761ef2f4d8952857028c6541b908e985`. Não cadastrar rs5 nem reutilizar outra identidade. O retorno do operador comprova dez registros, SHA `a5f9de948ab3fcf15b2657061d540dcbbafda98e1dd7a68137e617b58a894843`, recarga `2026-10-08T00:39:18.786312Z`, PID 1278094. DLL ativa declarada: fonte `ab192bf1585e30f303d041f13b36a1f9c96d2caa`, hash `815fc8bc99a9d16247488a1797d2726b928eb3e592fd8a19700371e8d57c3243`. São evidências daquele retorno, não medição atual do Linux por este PC.

A assinatura do APK permanece `7b16ee1aca7db7a50e7cc6c8612cf2a3568f474894a468865d842bf720c89825`. A nova identidade do arquivo APK é registrada na entrega; a validação dos contratos existentes permanece. Nenhum serviço Linux, banco, licença ou outro produto foi alterado.

## 4. Compilação reproduzível

1. `recipes/build_candidate.py --baseline --output <E:novo>/baseline`: recompila a composição R74 e exige DEX28 e DEX35 idênticos aos instalados.
2. `recipes/build_candidate.py --output <E:novo>/compiled-final`: valida hashes dos 201 Java, aplica somente o arquivo declarado em `JAVA-OVERLAY-MANIFEST.json`, compila API34/Java8 e D8 minAPI26.
3. Executar regressões, sessão, pump e transporte pelas receitas desta pasta. Cada recibo identifica seus inputs e alcance. O teste de sessão mantém a pasta padrão E: indicada no código.
4. `recipes/package_r76.py` exige os recibos revisados e seus hashes em `evidence/package-gates.json`. Usa o APK R75 exato e troca apenas `classes35.dex`; compara todas as outras entradas byte a byte. Exige certificado original e alinhamento 16 KiB.
5. `recipes/install_verified.py`: escolher serial explicitamente; recusar emulador ativo; instalar por streaming `-r --no-incremental`, conferir hash integral, UID e data original. Não desinstalar, limpar dados ou copiar outro APK ao armazenamento do telefone.

DEX35 corrigido: `c03ea2f4aa30c5e32654c575115583f72815b9701c16791c4f94c6ade753f2ff`. DEX28 preservado: `1a04582a02804ecbe4b075dd487118a171b0b0f305dbb8884015bbede09b25d7`. Os testes TLS usam exatamente os JARs que geraram esses DEX, conferidos por hash.

## 5. Evidências e limites

| Ensaio | Resultado | Limite |
|---|---|---|
| Reprodução DEX R74 | Ambos DEX byte a byte idênticos | Identidade de compilação; não gameplay |
| Regressão Java | 1.206 verificações | JVM |
| Sessão/saída/serviço vinculado | 101 verificações + 42 guardas | Android/Binder/HTTP modelados |
| Corrida do pump | 1.466 verificações / 143 casos; antiga falha 64/64, R76 atende 64/64 sem novo tick | Método real extraído; WebSocket/JNI modelados |
| Repetição concorrente | 20 execuções / 2.860 casos passaram | Repetição da mesma cobertura; contagem de checks pode variar conforme agrupamento dos produtores |
| TLS/WSS/TCP padrão | 4 execuções, cada uma 32 vetores + 46 verificações / 1.620.000 bytes | Duas fontes servidor, logging ligado e desligado; pausa nativa simulada |
| TLS/WSS/TCP com estresse | 2 execuções, cada uma 32 vetores + 72 verificações / 12.168.608 bytes | Fonte da DLL ativa `ab192bf`, logging ligado e desligado; não telefone |

A matriz completa transferiu **30.817.216 bytes exatos**. Inclui envio bidirecional simultâneo, escrita de 8 MiB com bloqueio real por crédito esgotado, recuperação após cortes de host/convidado/ambos, mesmos sockets TCP, sala/geração, ACK/replay, assinatura RSA e recusa de ticket sem prova. Processos locais foram encerrados e fixtures de credenciais sintéticas removidas. Não publicar os logs privados dessas execuções.

Os testes usam `ab192bf` (produção declarada) e `32ce9d2` (base anterior). O export de 60 inputs foi comparado: a diferença é `StationOnline.cs`, relacionada aos perfis; Relay permanece igual. **Não testam nem homologam a candidata servidor `6f27c6c`.** O fato de TLS local passar com logging desligado nesta matriz não encerra a falha relatada naquela candidata.

## 6. Ações e respostas necessárias do servidor

1. Ler esta entrega e conferir que APP-01 agora está no DEX35; não solicitar novamente uma correção já integrada. A confirmação física nos aparelhos permanece separada.
2. Preservar o cadastro rs4 atual; não reiniciar o serviço só por esta atualização Java. Não ampliar capacidades por hipótese (v2 declarado: 64 salas/128 participantes, janela 256 KiB por direção, recuperação em RAM).
3. Prosseguir na análise do timeout TLS da candidata `6f27c6c`/DLL `71ba30b8ca4b363344facc6ff750be6886183d576ef0a0456b57086d3b83d14e`. Exigir explicação reproduzida, gates sombra/público e ativação coordenada antes de declarar SRV-01/02/03 em produção.
4. Em nova partida coordenada, correlacionar primeira causa antes/depois de Detach, tipos/papéis dos epochs e métricas DATA/PONG/filas/GC/TCP. Nunca preencher métricas antigas ausentes ou atribuir toda demora ao servidor com base em RTT.
5. Publicar resposta como **SERVIDOR → APP**, com branch, commit completo, fonte/DLL/registro realmente ativos, instante/PID, comandos sintéticos e evidências saneadas. Nosso handoff não é essa resposta.

Não remover Cloudflare, reduzir TLS/pin/proof, alterar timeout por tentativa, descartar inputs tardios nem desligar autenticação. Login/catálogo/capas/downloads/controles e os outros produtos permanecem intactos.

## 7. Conferência física pendente

Os recibos de instalação indicam exatamente quais aparelhos receberam R76. Instalação e entrada autenticada não comprovam partida prolongada. Usar sala nova, ambos papéis, movimentos simultâneos, áudio/imagem, saída/reabertura e interrupção controlada de rede. Medir no mesmo intervalo app e servidor. Não marcar a revisão como estável geral nem prometer FPS/temperatura percentual com base nas fixtures.

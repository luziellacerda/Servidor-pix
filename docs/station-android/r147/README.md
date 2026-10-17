# R147 — backup completo conferido

Backup da candidata fonte `64a6da91aee9c61d26a3a85172af6521fcc82a36`, APK `c038a0d75113359187d8eb9d2762e8d7e4ceee1f3538d1982313fc1e3a781ad6` e kit `38041c427894dfd502bcc0d6859ca036630ebaab6c8df1ecd05f65a944f201ee` concluído: 57,665 arquivos/8,294,993,331 bytes verificados, incluindo 33.847 inputs originais do motor. Os dois bundles Git foram recuperados em repositórios bare novos, com fsck completo estrito, sem alternates, remotes ou reflogs; snapshots app `71873f584b503b825a1b1827f840bf54d6d9c22c` e servidor `c5ba9d636ffe7b98a07f55dfc252d49254e5633b` preservados. As dependências completas R146/R145 foram revalidadas; ferramentas/NDK e autoridade privada mantêm as referências aos checkpoints obrigatórios. R143 permanece a referência estável. A conclusão do backup não confirma instalação, partida ou aplicação do kit no servidor; o aceite para consumidor continua pendente. Ler r147/evidence/r147-backup-complete-summary.json. O resumo anterior de backup pendente e os demais históricos permanecem íntegros.

## Histórico preservado integralmente

## Retornos recebidos: 01h UTC e ramo histórico de downloads

O recibo de 14/10/2026 às 01:05 UTC (app `8f7f8289be2318825be9843f14bd9fe34bb1534f`) reconheceu R145/R146 e a rota v3 direta da R146. Ainda descrevia a primeira fonte R147 6b em compilação; esse estado é histórico. A R147 final atual continua na fonte `64a6da91aee9c61d26a3a85172af6521fcc82a36`, APK `c038a0d75113359187d8eb9d2762e8d7e4ceee1f3538d1982313fc1e3a781ad6` e kit `38041c427894dfd502bcc0d6859ca036630ebaab6c8df1ecd05f65a944f201ee` de 12.183.965 bytes. STATUS, manifesto USB, fontes congeladas e BUILD-PROGRESS histórico permanecem intactos.

Naquele ciclo a autenticação Linux falhou e não houve snapshot protegido fresco nem cadastro. A leitura protegida das 23h havia concluído; 39 motores/11.604 perfis/6.743 modos no retorno 01h são evidência anterior, não baseline atual. O catálogo 47 pertence ao retorno posterior das capas N64; a precedência das artes USB foi conservada. Zero kits encontrados no escopo acessível e 91 diretórios inacessíveis não provam ausência em todo armazenamento. Para concluir, entregar somente o ZIP R147 atual, autenticar localmente, obter baseline fresco, conferir seis ELF/20 dependências/quatro IDs/78 modos e registrar aditivamente pelo preparador e registrador vigentes sob scan.lock. Não há nova DLL, deploy/restart ou migração genérica exigidos por esses retornos. Cadastro aplicado e gameplay continuam sem comprovação correspondente.

O ramo de downloads `f5145968ec86931186f99ebfe4af172b71580ff8` registra o relato prático de ganho superior ao dobro/pico aproximado de 10 MB/s na candidata histórica e696 sobre R139. Não houve comparação controlada nem SHA instalado obtido por ADB. A contraparte `c8817a2c0e6bfadef7c422cd041c431697f70410` registrou aquela composição no catálogo 46, com 35 motores; não registra a R147 atual. O teste protegido de 4 MiB foi em vhost local, sem medição WAN Android ou de partidas. Preservar esse relato como histórico; não sobrescrever o manifesto R147 pelo manifesto e696 com adbPath vazio nem executar seus operadores antigos. Ganho de download não mede RTT/FPS/gameplay. Ler evidence/server-return-20261014-01z-summary.json, evidence/historical-direct-download-branch-audit.json e evidence/current-kit-receiver-action.json; fontes e hashes em evidence/latest-return-source-sha256.json.

### Corpo anterior preservado integralmente

# R147 final — inicialização e saída Dolphin, candidata compilada

Fonte final `64a6da91aee9c61d26a3a85172af6521fcc82a36`: 52 arquivos congelados, 48 inputs produtivos. APK privado SHA256 `c038a0d75113359187d8eb9d2762e8d7e4ceee1f3538d1982313fc1e3a781ad6`, 2,715,089,935 bytes. Assinatura original v2/v3, alinhamento de 16 KiB e todas as 13.860 entradas conferidos. A leitura USB atual registrou 0 aparelho(s), sendo 0 autorizado(s). Instalação desta revisão conferida em 0 modelo(s) pelos recibos fornecidos; a leitura USB não prova instalação. Testes físicos, aceite visual/online e repasse ao consumidor continuam pendentes; não declarar todos os jogos liberados. R143 continua a referência estável.

## Reparos restritos e reconstrução qualificada

Uma linha em `NativeLibrary_RunNetPlay` limpa `s_is_booting` quando BootSessionData é nulo, antes do callback de encerramento. O caminho normal, Run/Stop/Surface e CPU/JIT/caches permanecem. Antes da correção, a reconstrução integral reproduziu exatamente o core base `f7ebe048e030a029c0667cf071ab4a49d702f77daf50d40ad40ed1f493ded37d`, o arquivo completo `92b1951facb9fca6aad644026c47adcc62e09c9b5aca61629aab7e1515a270fe` e os quatro hooks. Core final qualificado: `3438ce727ab3a0d6f6fb4aa2a1b8068be5d17360e6c7aa0b8ff3594dbfde7c29`. Os 24.878 arquivos originais, 28 arquivos de dependências, 11 ferramentas e 4.590 pins do grafo foram preservados. Todos os 94 inputs gerados da base passaram; na versão corrigida somente MainAndroid.cpp.o muda, com os outros 93 byte exatos. Os ajustes de encaminhamento CMake e mapeamento de caminhos recompõem os bytes/metadados originais e não mudam opções de execução.

No DEX10, somente `NetplaySession` muda nos quatro métodos consumeBootSessionData, releaseNativeResources, isLaunching e onBootGame. Transferência/descarte do ponteiro e callback tardio são protegidos por três monitores curtos; JNI, callbacks e espera por threads ficam fora desses monitores. Há 4.991 classes, 4.990 preservadas; 40 campos/63 métodos mantidos, com 59 métodos intactos. Inversão integral real passou. Nenhum método/campo/classe, thread, timer ou JNI por quadro foi acrescentado.

Seis entradas substituídas: DEX10, core Dolphin, três identidades existentes e engines.json. As outras 13.854 entradas e 39 DEX são iguais à R146; zero adições/remoções. O mapa compartilhado mantém 345 chaves e altera somente DEX10. O core usa seus vínculos separados existentes: compiledBackendSha256 de N64/Dolphin, coreSha256 Dolphin e descritores GC/Wii. As inversões recuperam integralmente os objetos anteriores. PSX/N64 conservam seus próprios cores; opções, controles, capacidades, perfis e READY/ACK não foram ampliados.

Visual R145, carrossel `2e15a9a330c2562c71574921f5558d6142e3051bba64a315434e5714f38908a9`, rota direta v3 R146, API/login/downloads/relay legado, HLE/multitap/g52 e demais motores/reparos permanecem no pacote conferido. Não há medição física nova de CPU/FPS/latência nem prova de melhoria de velocidade das partidas.

## Servidor: aplicar somente o kit correspondente

Kit privado atual SHA256 `38041c427894dfd502bcc0d6859ca036630ebaab6c8df1ecd05f65a944f201ee`, 12,183,965 bytes, limite de 100 MB conferido. São seis bibliotecas/20 dependências, quatro IDs atuais e os mesmos 78 modos revisados: 66 N64, nove GameCube, três Wii. Zero novas aprovações PS1 HLE por edição. O core e os bindings atuais vêm do APK/recibo final; o kit das revisões anteriores não substitui este. Preparado não significa aplicado e não há retorno aplicado correspondente registrado aqui.

Esses 78 modos abrangem somente 44 IDs/edições, com 43 textos de título: 33 N64, oito GameCube e três Wii. Extreme-G tem duas identidades de conteúdo; não deduplicar por nome. Há 53 modos/32 edições para três participantes e 54 modos/33 edições para quatro. Não são 78 jogos nem cobertura completa do catálogo; todos os 78 registros carecem de validação física. No último catálogo N64 documentado (47), 124 das 157 edições ficam fora deste ledger, o que não significa automaticamente incompatibilidade ou multiplayer. Os snapshots históricos GameCube/Wii/PS1 (44/46) tinham 21/7/84 edições, com 8/3/0 revisadas aqui; o catálogo completo atual dessas outras plataformas não foi verificado por este registro. Os owners mantêm N64/GC/Wii até quatro e PS1 até dois controles; este ledger tem zero modos de cinco participantes.

As recusas de controles são por perfil/modo: Mario Party 7 possui modo aprovado com quatro controles independentes, embora o modo compartilhado não seja implementado. Four Swords/GBA-link e Galaxy/Co-Star exigem seus adaptadores específicos. A revisão PS1 consolida 26 registros, dez simultâneos e 16 pendentes de fonte primária; o resumo histórico que dizia 20 pendentes fica preservado e a discrepância é registrada, sem converter contagem em aprovação HLE. Para ampliar cobertura, revisar cada edição/conteúdo/modo e provar inputs humanos distintos no owner final; adaptadores/capacidade de cinco jogadores exigem escopo autorizado separado. Ler evidence/final-online-coverage.json e evidence/consumer-coverage/RELATORIO-COBERTURA-HANDOFF.txt, com o inventário sanitizado de 269 edições e as fontes SHA.

Importar a função vigente `prepare_independent_owners.prepare` com sua assinatura real: ela já chama o merge com `migrate_existing_native_profiles=False`. Não acrescentar esse keyword à assinatura externa nem executar o main de operadores históricos. Usar catálogo/perfis/modos atuais protegidos e a lista revisada exata. O registrador existente confere os binários recebidos, acrescenta novos IDs imutáveis e preserva os anteriores, sob autenticação Linux nativa/scan.lock/rollback. Preservar licenças, perfis, modos, API, registros anteriores e QoS; não reaplicar R141, promover HEAD inteiro ou implantar/reiniciar serviços para este pacote. O registro online aplicado permanece R141; o catálogo 47 veio no retorno posterior das capas N64. Obter o retorno aplicado e capabilities assinadas exatas antes de declarar acesso homologado.

Para PS1, HLE exige revisão por edição/conteúdo e dois inputs simultâneos reais; ausência de cadastro não autoriza aprovação genérica, troca de BIOS/controles ou multitap. N64/GameCube/Wii precisam de sessão Android real. O resultado de Top Gear 3000/Bomberman com três participantes na R143 fica no histórico e não homologa as novas plataformas.

## Recuperação e histórico

O backup R147 ainda não tem validação completa correspondente registrada neste handoff. R146 tem backup completo verificado de 3.619 arquivos, dois bundles recuperados e fsck estrito; a dependência R145 revalidou 3.632 arquivos. R143, seus tags, backup completo e ponteiros permanentes permanecem intactos. Não promover esta candidata como estável sem os testes necessários.

A primeira tentativa `6b4eab2c9221bb698362547df822962cda81cee9` permanece histórica: não qualificou o hash integral por perda de 37 linhas vazias de zconf.h e descarte do mapa de caminhos no alvo mGBA. A correção foi congelada no escopo final distinto; as provas reais recompostas não substituem nem reescrevem aquele registro. `BUILD-PROGRESS.json` continua o progresso histórico da primeira tentativa, com seus dados/flags originais. O estado atual é `STATUS.json` e as evidências finais anexadas.

## Testes e limites do repasse

Instalar a mesma candidata assinada em todos os participantes ociosos, como atualização que preserva UID/dados/primeira instalação; não desinstalar nem limpar dados. BAT/PowerShell originais permanecem iguais, selecionados pelo manifesto explícito de teste. Registrar título/edição/modo, criação/entrada/boot, controle independente de cada jogador, perda de sinal, cancelamento, saída e retorno às salas. Quantidades maiores dependem do jogo/perfil aprovado. Compilação, cadastro, transporte, abertura do frontend e gameplay são etapas distintas.

As evidências públicas abaixo contêm somente hashes, contagens e estados sanitizados. APKs, ZIP privado, ROM/BIOS, chave/licenças privadas, identificação de aparelhos, códigos de ativação e logs brutos não são copiados ao Git por este gerador.

## Registro anterior preservado integralmente

## Atualização: primeira base rejeitada pela conferência

A primeira reconstrução terminou, mas não reproduziu o hash completo obrigatório. A comparação encontrou duas causas na receita:37 linhas vazias removidas de zconf.h e mapa de caminhos descartado pelo alvo mGBA. Código executável e dados de runtime comparados permaneceram iguais; os dados de depuração e o identificador da compilação diferiram. A correção da receita será congelada em escopo final distinto, mantendo esta tentativa e os gates92b/f7. Nenhum core produtivo/APK R147 instalado ou kit aplicado. Ler evidence/native-baseline-attempt1.json.

O backup completo R146 foi concluído:3.619 arquivos, dois bundles com recuperação real em bare novo e fsck completo estrito; R145/R143 e ponteiros estáveis preservados. Ler evidence/r146-backup-summary.json.

## Registro anterior preservado

# Preparação R147 — fechamento do trabalho visual e online

Estado de compilação: fontes congeladas e publicadas no commit `6b4eab2c9221bb698362547df822962cda81cee9`. DEX10 compilado e conferido, SHA `4d200405747b4d42108fe6bd8afdccc290db3fa1a72a9aa28c779f8772a53a78`, 6.357.400 bytes. Motor: configuração concluída, reconstrução integral da base em andamento. APK, instalação, kit final aplicado e gameplay da R147 ainda não comprovados. Ler BUILD-PROGRESS.json. Este registro de progresso permanece como histórico depois da conclusão.

## Continuidade e pontos de recuperação

A estável R143, seu tag, APK e backup permanecem preservados. A R145 contém o azul céu e as faixas solicitadas; a R146 mantém esse visual e integra somente a conexão v3 direta. As duas já foram compiladas e assinadas. A R147 acrescenta somente os defeitos comprovados abaixo. Cada etapa tem fonte congelada e provas próprias; revisões anteriores não devem ser recompiladas usando fontes posteriores.

O histórico completo continua no handoff existente e nas pastas r143/r144/r145/r146. Fontes executáveis, receitas e documentação vão ao Git; APK, kit privado, ROM/BIOS, ferramentas, chave, registros e identificação de aparelhos ficam no armazenamento privado.

## Defeitos Dolphin comprovados

1. `NativeLibrary_RunNetPlay`: quando `BootSessionData` é nulo, a rotina terminava sem limpar `s_is_booting`. `SurfaceDestroyed` aguardava esse estado. A correção acrescenta uma única limpeza antes da mensagem e do callback de encerramento. A inversão deve recuperar todo o arquivo MainAndroid.cpp original; Run, Stop, Surface e o caminho normal permanecem.
2. `NetplaySession`: consumo e descarte concorrentes podiam obter o mesmo ponteiro, e um callback tardio podia fornecer um recurso depois do fechamento. A correção restringe quatro métodos existentes: consumeBootSessionData, releaseNativeResources, isLaunching e onBootGame. A posse passa a ser transferida atomicamente por monitor curto da classe. JNI, callbacks e espera por threads ficam fora do monitor; closeBlocking e seu monitor original não mudam.

Os testes isolados demonstram esses defeitos e a correção proposta. Eles não estabelecem que sejam a causa de toda falha relatada nos telefones. A R147 só pode ser declarada compilada depois dos recibos reais do motor, DEX, identidade e APK.

## Conferência da reconstrução

O motor original deve reproduzir exatamente o core f7ebe048 e o arquivo completo 92b1951, com quatro bibliotecas auxiliares iguais. Os 24.878 arquivos originais, 28 arquivos de dependências, 11 ferramentas e 4.590 entradas do grafo ficam pinados. Os 94 arquivos gerados incluem arquivos compilados: a base deve reproduzir todos; a versão corrigida só pode mudar o objeto MainAndroid correspondente à linha autorizada. Os outros 93 permanecem iguais. A fonte original é somente leitura; operações de empacotamento são redirecionadas para a pasta nova.

O DEX10 tem 4.991 classes. A montagem deve preservar 4.990 e todos os 40 campos/63 métodos da classe alterada, exceto os quatro corpos aprovados. A representação real deve passar na inversão integral. Não há métodos, campos, classes, threads, timers ou chamadas JNI por quadro novos.

São previstas seis substituições no APK: DEX10, core Dolphin, três identidades existentes e engines.json. As outras 13.854 entradas e 39 DEX devem permanecer byte iguais. O mapa compartilhado mantém as mesmas 345 chaves e muda somente DEX10. O core é conferido separadamente e seus vínculos duplicados atuais são atualizados por inversão, sem ampliar configurações ou aprovações.

## Plataformas e servidor

- N64: motor real e execução determinística presentes. Não foi encontrada justificativa para alterar CPU/JIT. É necessário cadastrar a identidade correspondente ao APK final e confirmar os controles em aparelho.
- PS1: motor real presente. A lista atual não acrescenta aprovações HLE por edição. A [documentação do PCSX ReARMed](https://docs.libretro.com/library/pcsx_rearmed/) registra limitações de compatibilidade do HLE. Não habilitar genericamente jogos, BIOS alternativo ou multitap para encobrir falhas; verificar título, conteúdo e dois controles antes de aprovar a edição.
- GameCube/Wii: porta Android própria e pontes JNI presentes. O [guia oficial do Dolphin](https://dolphin-emu.org/docs/guides/netplay-guide/) informa que o NetPlay original não está disponível no Android; os testes desta integração são necessários. As correções acima tratam inicialização/saída, não demonstram gameplay por si só.

O último retorno do servidor conserva os registros R141, com 39 motores, 11.604 perfis e 6.743 modos; catálogo atualizado para 47 pelas capas N64 recebidas. O cadastro do kit final deve ser aditivo, mantendo os 78 modos revisados, registros anteriores, licenças, API e configurações. Não reaplicar R141, fazer migração genérica ou reiniciar/deployar serviços para esta entrega.

O kit deve corresponder ao SHA do APK efetivamente instalado, incluindo core e dependências reais. Kits R143/R144/R145/R146 não substituem o kit R147. O retorno aplicado e as capacidades assinadas precisam ser conferidos antes de atribuir problemas de acesso somente ao app.

## Testes físicos ainda necessários

Usar a mesma versão final em todos os participantes. Registrar título/edição, aparelho, modo, etapa da falha, conteúdo, identidade/perfil, controles de cada participante, carregamento, perda de sinal, saída e retorno às salas. N64, PS1, GameCube e Wii precisam de sessão real com pelo menos dois controles independentes; quantidade maior depende do jogo e do perfil homologado. O resultado humano de três participantes em Top Gear 3000/Bomberman na R143 permanece no histórico e não homologa automaticamente as novas plataformas.

Instalação USB é atualização, com hash/certificado, estado ocioso, UID/dados/primeira instalação preservados. Os scripts BAT/PS1 existentes permanecem iguais; apenas o manifesto explícito de teste apontará para o APK final. Não declarar instalação enquanto nenhum aparelho autorizado estiver conectado. Não promover a estável antes da validação necessária.

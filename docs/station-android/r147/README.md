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

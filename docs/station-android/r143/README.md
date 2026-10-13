## R143 — teste humano com três jogadores concluído, 13/10/2026

Após o teste solicitado de Top Gear 3000 em Corrida VS, o mantenedor respondeu primeiro “entrou 4 jogadores” e depois confirmou “jogou 3 jogadore”. Registrar sucesso relatado de jogo com **três participantes** e desbloqueio da seleção. A primeira resposta não comprova quatro telefones humanos. [Resultado e limites](evidence/three-player-human-result.json).

A R143 permanece instalada no g52, com APK/fonte e preservação de dados conferidos. Na leitura passiva posterior, o g52 já estava no frontend em uma sala de Super Bomberman 3, sem processo station_netplay ativo; não houve captura da corrida ou medição dos controles independentes. Captura privada preservada. Resultado humano não equivale a instrumentação Android nem homologa outros jogos, quatro/cinco jogadores físicos, estabilidade prolongada ou queda de sinal. Avisos JNI anteriores continuam sem causa isolada.

A correção localizada remove a duplicidade da opção que impedia a leitura do remapeamento multitap SNES. Motores, downloads, saída, FPS, READY/ACK e demais métodos permanecem conforme os recibos. Kit dos quatro marcadores independentes preparado e ainda não aplicado; SNES mantém cadastro existente. APK de testes e instalador continuam R143. Nenhuma recompilação para registrar este resultado.

## Histórico preservado

## R143 instalada e frontend observado no g52 — 13/10/2026

APK R143 `508480e3671ae924f037bd32f6d71efda7a9d905c45c7b2571bd9f219add83d4`, fonte `26257869b59af8ecea44932ab287705990a9acbe`, instalado no moto g52. Hash integral/UID/dados/primeira instalação preservados e frontend oficial observado. Aparelho ocioso, sem jogo/download/serviço antes da atualização; nenhum force-stop ou limpeza de dados nesta instalação. [Recibo](evidence/installation-moto-g52.json).

O leitor real confirmou que a opção duplicada introduzida naR142 manteve a primeira ocorrência false. A R143 substitui essa linha por true uma vez, apenas para o perfil multitapSNES aprovado. DownloadsR141/HLE/saída/motores/FPS/READY e demais métodos preservados. A pesquisa web e testes de parser estão no registro abaixo; não atribuir causa por analogia com o issue externo.

Teste humano solicitado: g52 anfitrião, Top Gear3000 Corrida VS3, seleção3 e comando independente do terceiro carro. Resultado ainda pendente; quatro jogadores não testados. A56/Edge30 permanecemR141, core/runtime/perfilSNES exatos; não inferir gameplay por igualdade de hashes. Kit independenteR143a4ba2105 preparado e aguardando cadastro dos quatro marcadores renovados; SNES não requer registro novo. Não deploy/restart, nem promover recuperação permanente. BAT de testes R143, scripts preservados; fonte/recibos noGit e APK/registros/kit privados no backup F existente.

## Histórico preservado

## R143 — pesquisa web e correção da flag duplicada, 13/10/2026

**R142 falhou no teste físico de Top Gear 3000 Corrida VS3.** O mantenedor confirmou que não consegue selecionar3; captura e logs privados preservados. Não homologar a R142 com base no teste host anterior. A pesquisa encontrou [relato semelhante RetroArch10424](https://github.com/libretro/RetroArch/issues/10424), [multitap na porta2 no core](https://docs.libretro.com/library/bsnes_mercury_performance/) e [distinção entre dispositivos locais e solicitações da rede](https://docs.libretro.com/guides/netplay-multiple-controllers/). Relato histórico e workaround não comprovam a causa deste aparelho nem justificam mudar servidor/READY/motor.

O cruzamento encontrou um erro introduzido pela R142: a configuração continha auto_remaps_enable=false e uma segunda ocorrência true. O parser do runtime preserva a primeira chave. Seis corpos reais do parser e sua tabela hash executaram três cenários: false→true continua false; true único habilita; false único permanece false. Headers/fonte conferidos contra o manifesto correspondente. Inclusões/referências foram stubs que falham, nunca chamados; tokenizer adaptado ao CRT Windows no fixture. Teste não leu configuração privada Android nem executou corrida.

A R143 substitui a linha false existente por true uma vez, somente no helper SNES/perfil multitap assinado. Ausência da política recusa a preparação. Arquivo .rmp, vetor[1,257,1,1,1], guardas, modos e métodos originais preservados. Fonte final congelada `26257869b59af8ecea44932ab287705990a9acbe`, APK `508480e3671ae924f037bd32f6d71efda7a9d905c45c7b2571bd9f219add83d4`, 2.715.024.399bytes, assinatura originalv2/v3/16KiB. [Estado e recibos](STATUS.json).

Cinco substituições, zero adições/remoções,39DEX e13.855entradas preservados. DownloadsR141, opçõesHLER140, saída, core/runtime, FPS, botões, transporte, READY/ACK e perfis exatos. Preparações d6479ab/b02a080 e a montagem recusada permanecem para auditoria: o assembler gerou nomes de branch/goto no mesmo endereço; o guard final unifica somente rótulos consecutivos sem instrução antes de comparar todas as instruções/destinos. Nenhum APK anterior da R143 foi instalado.

SNES conserva seus IDs/perfil e não requer cadastro novo. Três marcadores/quatro IDs independentes foram renovados por vincularem DEX35; kit privadoR143 `a4ba210580ea68beb0086c2c986c6a884e888a1e9ad599bd7e9d9ae33364f606`,12.161.998bytes,38arquivos,6bibliotecas/20dependências, renova apenas os mesmos78modos. Preservar cadastrosR141/anteriores e usar a lista fixa corrigida, sem migração duplicada. PS1 zero novas aprovaçõesHLE. Kit preparado não é cadastro aplicado; nenhuma API deploy/restart.

Instalação R143 no g52 em andamento; publicar estado concluído somente com SHA/UID/dados/primeira instalação conferidos. A56/Edge30 continuamR141. Teste humano3/4 permanece necessário. Os logs atuais também contêm avisos JNI StackOverflowError na impressão de exceções, sem stack causal; investigação separada, sem atribuir à configuração ou alegar ANR corrigido. Capturas/logs/seriais/chaves/ROMs/BIOS/binários privados foraGit. BAT de testes aponta R143 com scripts byte exatos; ponteiros permanentes intactos.

## Histórico preservado


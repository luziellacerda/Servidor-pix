# R145 — azul céu e faixas dos consoles

## Estado verificável

Fonte executável congelada `25d10b2322d2e5687c9dfacc71a826c3955fad5d`. APK privado SHA256 `275f31b34145eb77c457b1a9c09bbff4034cc28a990b74121b0b1fd4573c5df0`, 2,715,057,167 bytes. Assinatura original v2/v3 e alinhamento 16 KiB conferidos. Candidata de teste; R143 continua estável e seus pontos de recuperação permanecem intactos. Instalação: pendente; nenhum telefone conectado na última leitura USB. Aparência física ainda não aprovada.

## Pedido atendido no código

Azul céu `#287EB5` → `#5DAEDE` em todos os fundos azuis existentes: principal/carrossel, salas online, downloads e painéis azuis Sony. Fundos neutros, verde/preto, cartões e textos conservados. As salas mantêm as cores/desenho das ondas da R144; o novo pedido esclareceu também o fundo azul mais claro.

| Sistema | Faixas na ordem |
|---|---|
| SNES | vermelho, vermelho, cinza |
| NES | roxo, vermelho, cinza |
| N64 | amarela fina, verde, azul, vermelho, amarela fina |
| GameCube | amarelo, verde, roxo, cinza |
| PSX / PS2 | vermelho, azul, amarelo |
| Dreamcast | verde, azul, vermelho, amarelo, laranja |
| FinalBurn Neo | verde, amarelo, laranja |

Outras dez famílias/paletas permanecem R144. Amarelas N64 usam metade da largura de cada faixa. Faixas decorativas não mudam número de jogadores.

## Preservação e compilação

Três DEX reais receberam dez literais em sete classes/nove métodos. A montagem foi decodificada novamente: inversão completa recupera as classes originais; 1.002 outras classes e 37 DEX integrais permanecem exatos. Activities históricas não foram recompiladas/restauradas. Saída R137, mitigação g52, HLE R140, multitap R143 e downloads R141 preservados.

Carrossel reproduziu integralmente R133 → R143 → R144 com 158 entradas conferidas antes do overlay de três cabeçalhos. Os 33 exports, dependências, SONAME e 16 KiB permanecem. Ondas mantêm curva/tempo/32 segmentos e buffer fixo reutilizado de 66 vértices; N64/Dreamcast agora usam cinco desenhos/330 vértices versus três/198, trabalho solicitado, sem novo timer/thread/heap. CPU/temperatura físicos ainda não medidos.

APK final: oito substituições, 13.852 entradas preservadas, zero adições/remoções. Motores reais, opções, controles, READY/ACK, FPS e métodos de rede não alterados por esta etapa. Quatro identificadores independentes renovados apenas pelo vínculo obrigatório aos quatro payloads visuais; nove descritores RetroArch mantidos. Scripts BAT/PowerShell originais preservados; manifesto explícito de teste aponta R145.

## Handoff do servidor comparado

Retorno `c926aa9`, ciclo 23z: cadastro efetivo ainda R141, 39 motores/11.604 perfis/6.743 modos; API DLL ef5270b3 e PID2867363 mantidos. Kit R143 não encontrado no Linux. Esta R145 exige seu próprio kit: SHA256 `89c7fb016ab23a3ecf050e1ce2f83a3e0dfecfb93eb5763a895f3c5beac50fb8`, 12,171,175 bytes, seis bibliotecas/20 dependências e os mesmos 78 modos (66 N64, nove GameCube, três Wii). Não misturar R143/R144/R145, não reaplicar R141 nem operadores históricos/main. Preparador atual com `migrate_existing_native_profiles=False`, baseline protegido atual e scan.lock; preservar cadastros/licenças, sem reiniciar API por esta entrega. Kit preparado não comprova aplicação.

## Próxima etapa online solicitada

N64, PS1, GameCube e Wii têm motores reais no APK. Existência do motor não comprova compatibilidade/startup. Faltam o vínculo do servidor ao APK final e testes de jogos específicos; PS1 tem zero novas aprovações HLE por edição nesse owner. Não liberar todos os modos sem preservar o contrato de conteúdo/firmware/controles. Investigar o primeiro estágio que falha em cada plataforma e corrigir somente a causa demonstrada. A rota WSS v3 direta pedida no retorno continua fora da R145; integração requer URI e factory TLS correspondentes, mantendo login/comandos/catalogo/v1 na origem existente.

Referências primárias externas para investigação: [guia NetPlay Dolphin](https://dolphin-emu.org/docs/guides/netplay-guide/) distingue suporte desktop/Android padrão; o Station possui integração nativa própria que deve ser verificada. [PCSX ReARMed](https://docs.libretro.com/library/pcsx_rearmed/) documenta limitações de compatibilidade HLE. Essas referências não demonstram a causa de uma partida específica.

APK, chaves, BIOS/ROM, registros brutos, capturas, código de ativação e seriais continuam privados. Nunca reaproveitar o código de ativação já consumido do A56. Atualizações devem preservar UID/dados/primeira instalação; a instalação nova histórica R144 teve preservação inaplicável após ausência prévia do pacote.

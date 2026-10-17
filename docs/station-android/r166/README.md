## Atualização: R167 aplicada

A R166 não foi aplicada. O retorno combinado R167 foi cadastrado diretamente;
ler [recibo R167](../r167/server-return.json). Não executar `authenticate-next.py`
da R166. O preparo abaixo é histórico preservado.

# R166 — kit recebido e cadastro preparado; autenticação pendente

**Não aplicado.** O kit privado R166 `b2b4eb84` foi recebido e conferido neste Linux:
12.988.932 bytes, 45 entradas, 44 hashes e 27 ELF ARM64 reais. Os seis binários
principais, 21 dependências e a cadeia dos recibos do APK assinado foram conferidos.
Nenhum APK foi recebido ou inspecionado aqui; a assinatura e o SHA do APK são
evidência documental da produtora. O [handoff original](HANDOFF-R166-PSX-ABRIR-PARTIDA-20261015.md)
permanece intacto. Ler o [retorno real](server-return.json).

O preflight do registrador vigente passou para quatro owners e 492 perfis/modos
aditivos. PSX: 84 edições, 93 decisões, 23 modos2P em19 edições aprovadas,
17 impedidos e53 classificações sem vagas. O ledger HLE real foi reavaliado para
o mesmo core e opções, comparando também as decisões R165 ativas. N64:387 perfis,
230 modos multiplayer/157 individuais; GameCube9/Wii3. Não há nova aprovação HLE.

A primeira projeção recusou WinBack por420 bytes acima de128KiB. Somente as
instruções repetidas de seis vínculos R166 novos foram condensadas; capacidades,
modos, fontes, fingerprints e todos os registros antigos permaneceram exatos.
O maior grupo ficou em130.472 bytes. Validador, limites e DLL não foram alterados.
Regressões recusaram core diferente, conteúdo alterado e HLE real ausente.
87 motores/16404 perfis/11489 modos são **projeção não aplicada**, sobre o export
R165 anterior. Não representam uma baseline protegida fresca deste ciclo.

A janela Linux nativa expirou em115,18s sem root. Somente o próprio pkexec ainda
não autenticado foi cancelado; nenhuma operação root começou. Não existem
native-started, candidate, registrar-applied ou data-applied neste diretório.
Nenhum timer foi parado. ExecStart/processo ainda indicam DLL53d4/PID152812,
mas DLL/configuração/registros protegidos não foram lidos agora. R165 permanece
a última aplicação comprovada; suas provas e a saída confirmada estão preservadas.

Próximo ciclo: conciliar esses arquivos e as autoridades aplicadas, comparar
novos refs/kit/fontes e conferir `sealed-inputs.json`. Se a preparação continuar
atual e não existir operação iniciada/aplicada, executar:

```bash
python3 -B /mnt/DADOS/station-maintenance-20261015-20z/authenticate-next.py
```

Esse controlador de uso único abre autenticação nativa, limita a espera pré-root
a115s e nunca interrompe root iniciado. O operador novo `apply-r166.py` exige
baseline protegida fresca da DLL53d4/configuração/registros, usa o registrador
vigente sob scan.lock, para apenas o timer scanner na publicação e aguarda seu
oneshot. Só o cadastro real será seguido das capabilities assinadas das84PSX e
157N64 locais/públicas, admissões/recusas, GC/Wii e transporte sintético. Se surgir
qualquer recibo de aplicação ou erro após root, conciliar; não repetir cegamente.
Não executar operadores R165/R164/PSX históricos nem implantar DLL.

Capas Git e manifesto SNES continuam iguais; zero importações. Faltam os três
PNGs finais exatos já registrados13z, e20 artes continuam sem edição exata.
Não substituir as capas corretas13z/1080R164 por aproximação. Preservar PS3/PS4
offline,502 histórico, sinopses/campos editoriais e testes físicos pendentes.
Após cadastro aplicado, dois aparelhos na mesma R166/edição/modo aprovado devem
testar primeiro quadro, áudio, controles e saída. Nada aqui homologa gameplay.

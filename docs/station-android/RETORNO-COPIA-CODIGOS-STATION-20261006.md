# Copiar código Station: compatibilidade publicada — 06/10/2026

Publicado às17h59min30s de Maceió (`2026-10-06T20:59:30.889217Z`). Página: https://turbobox.lzgames.com.br/admin/station . Fonte`247bb0a33bbb800228d1bf1c9fba6ff88469c000`, após implementação`5c5234f93bca263f01ff798e832ffc6fc1afaadf`.

## O que mudou

O botão Copiar código agora tenta a API moderna e, se ela estiver bloqueada/ausente, copia por textarea temporária dentro do diálogo. Só mostra **Código copiado** após sucesso. Caso o navegador negue ambas, mostra **Não foi copiado — copie o texto selecionado**; o código permanece selecionado. A API antiga de cópia não persistia dados no navegador. Agora o campo temporário é removido e o foco retorna ao botão. Código vazio, fora do formato43Base64URL ou substituído durante a chamada não produz confirmação falsa.

A orientação do diálogo manda verificar Código copiado e colar no app. Cache do JS atualizado para`20261006-3`. Só `station-admin.php` e `station-admin.js` mudaram. Backend, cadastro, geração/consumo de códigos, licenças, limites por aparelho, chaves, SQLite/PostgreSQL, API, helper e APK não mudaram. Nenhum serviço foi reiniciado.

## Evidência e limite da conclusão

Chrome real:5cenários (API moderna, negada, ausente, ambas negadas, vazio), com leitura real da área de transferência conferindo exatamente maiúsculas/minúsculas, `_` e `-`; sem campo residual, bloqueio permanente de botão ou armazenamento do código. O mesmo teste falha na versão antiga nos cenários de API negada/ausente e vazio. PHP lint, sintaxeJavaScript, proteção contra sobrescrever release posterior e restauração dos dois arquivos passaram. HTTPS público conferiu hashJS; página autenticada conferiu a orientação. Prova pública: `COPIA-CODIGOS-STATION-PRODUCAO-20261006.json`.

O usuário informou **Entrou antes desta publicação**; o suporte confirmou licençaACTIVE/BOUND, código consumido e aparelho vinculado, com desafio/sessão/perfil/catálogo200. Não foi reemitido código nem alterada licença real por esta correção. A causa exata dos403anteriores não foi provada: a falha de clipboard foi reproduzida e corrigida, mas não deve ser atribuída retroativamente à tentativa real sem prova.

## Operação e restauração

Continuar usando Copiar código → confirmar Código copiado → colar no app. Código é de ativação,30min/uso único; não é senha administrativa. O app já ativado usa a licença salva; não gerar outro código para tentar resolver uma sala online.

Backup privado verificado: `/home/lz-servidor/station-clipboard-backup-20261006`, modo0700; resultado privado600 em`/home/lz-servidor/station-clipboard-result-20261006.json`. Publisher: `ops/station-admin/deploy-clipboard.py`. Restauração específica com autenticação nativaLinux:

```sh
pkexec /usr/bin/python3 ops/station-admin/deploy-clipboard.py --rollback
```

A restauração confere os arquivos/hashes/PIDs e recusa alterações posteriores; restaura somente os dois arquivos do site. Não restaurar bancos ou licenças sobre produção. APIa2bb176/DLLd181bf97/PID875574 e administraçãofe4b631/PID910766/helper910776 permanecem as publicações vigentes. Gameplay real emdupla está em diagnóstico separado no retorno Battletoads.

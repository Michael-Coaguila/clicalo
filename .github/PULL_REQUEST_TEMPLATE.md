<!--
Título (en inglés): Conventional Commits con los tipos y ámbitos de blueprint §13, por ejemplo
«feat(panel): add compact view». Es el mensaje del commit de squash.
Title (in English): Conventional Commits with the types and scopes of blueprint §13, for example
"feat(panel): add compact view". It becomes the squash commit message.
-->

## Qué cambia y por qué · What changes and why

<!-- Una o dos frases y el issue que cierra, por ejemplo «Closes #123». · One or two sentences and the issue it closes. -->

## Comprobaciones · Checklist

- [ ] `cl check` pasa en mi equipo: la última línea dice «cl check: correcto». · `cl check` passes locally.
- [ ] Cada requisito del catálogo que toca este PR tiene una prueba marcada con `[Trait("Req", "<ID>")]`, y ningún requisito se rebaja (las propuestas van a §6.1 del catálogo). · Every catalog requirement touched has a test tagged `[Trait("Req", "<ID>")]`; no requirement is weakened (proposals go to catalog §6.1).
- [ ] Los textos de producto nuevos o cambiados están en `data/i18n/strings.es.json` **y** en `data/i18n/strings.en.json`, sin literales en el código. · New or changed product text is in both JSON files, never a literal in code.
- [ ] Si toca un límite de confianza, un formato persistido o un contrato público, incluye o enlaza un ADR en `docs/adr/`. · If it touches a trust boundary, a persisted format or a public contract, it adds or links an ADR.
- [ ] No edité a mano nada generado. · Nothing generated was edited by hand.
- [ ] Mis commits llevan `Signed-off-by` (DCO; `cl setup` lo añade solo) y están firmados. · My commits are signed off (DCO) and signed.

## Accesibilidad · Accessibility

<!--
Si cambia la interfaz: cómo lo comprobaste (Narrador, Acceso por voz, Reconocimiento de voz de Windows, solo
táctil, alto contraste). Si no cambia, escribe «Sin cambios de interfaz».
If the UI changes: how you checked it (Narrator, Voice Access, Windows Speech Recognition, touch only, high
contrast). Otherwise write "No UI changes".
-->

## Nota para usuarios · User-facing note

<!--
Los PR feat, fix, a11y y perf que tocan src/ añaden un fragmento en changes/unreleased/ (con `cl note`, desde M2)
o llevan la etiqueta no-user-note.
feat, fix, a11y and perf pull requests that touch src/ add a fragment under changes/unreleased/ (`cl note`, from M2)
or carry the no-user-note label.
-->

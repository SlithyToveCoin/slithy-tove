// On small screens, the button opens the navigation below the site name.
// Without JavaScript, the links remain visible.
(() => {
  const header = document.querySelector('.site-header');
  const toggle = document.querySelector('.site-menu-toggle');
  const nav = document.getElementById('site-nav');
  if (!header || !toggle || !nav) return;

  const mobile = window.matchMedia('(max-width: 850px)');
  function setOpen(open) {
    nav.classList.toggle('is-open', open);
    toggle.setAttribute('aria-expanded', String(open));
  }

  toggle.hidden = false;
  header.classList.add('menu-ready');
  toggle.addEventListener('click', () => {
    setOpen(toggle.getAttribute('aria-expanded') !== 'true');
  });
  nav.addEventListener('click', (event) => {
    if (event.target.closest('a')) setOpen(false);
  });
  document.addEventListener('keydown', (event) => {
    if (event.key === 'Escape' && toggle.getAttribute('aria-expanded') === 'true') {
      setOpen(false);
      toggle.focus();
    }
  });
  document.addEventListener('click', (event) => {
    if (!header.contains(event.target)) setOpen(false);
  });
  mobile.addEventListener('change', () => setOpen(false));
})();

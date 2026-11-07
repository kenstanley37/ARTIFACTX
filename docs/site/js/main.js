// ArtifactX Official Website JavaScript
// Smooth Parallax, Interactive Tabs, and Scroll Animations

document.addEventListener('DOMContentLoaded', () => {
  // 1. Subtle, high-performance Parallax Background
  const backdrop = document.getElementById('parallaxBackdrop');
  if (backdrop) {
    let ticking = false;
    window.addEventListener('scroll', () => {
      if (!ticking) {
        window.requestAnimationFrame(() => {
          const scrollY = window.pageYOffset || document.documentElement.scrollTop;
          // Smooth 12% scroll rate for a gentle, cinematic cosmic drift
          backdrop.style.transform = `translate3d(0, ${scrollY * 0.12}px, 0)`;
          ticking = false;
        });
        ticking = true;
      }
    }, { passive: true });
  }

  // 2. Smooth Navigation Links
  document.querySelectorAll('a[href^="#"]').forEach(anchor => {
    anchor.addEventListener('click', function(e) {
      const targetId = this.getAttribute('href');
      if (targetId && targetId !== '#') {
        const target = document.querySelector(targetId);
        if (target) {
          e.preventDefault();
          target.scrollIntoView({ behavior: 'smooth', block: 'start' });
        }
      }
    });
  });

  // 3. Image Modal / Expand on Click for Showcase Previews
  const previewImages = document.querySelectorAll('.media-frame img');
  previewImages.forEach(img => {
    img.style.cursor = 'zoom-in';
    img.addEventListener('click', () => {
      window.open(img.src, '_blank');
    });
  });
});


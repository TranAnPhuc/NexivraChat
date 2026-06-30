import { useState, useEffect } from 'react';

export function useIsMobile(breakpoint = 768): boolean {
  const [isMobile, setIsMobile] = useState<boolean>(() => {
    if (typeof window !== 'undefined') {
      return window.innerWidth < breakpoint;
    }
    return false;
  });

  useEffect(() => {
    if (typeof window === 'undefined') return;

    const mediaQuery = window.matchMedia(`(max-width: ${breakpoint - 1}px)`);
    const handleQueryChange = (event: MediaQueryListEvent) => {
      setIsMobile(event.matches);
    };

    // Set initial value
    setIsMobile(mediaQuery.matches);

    if (mediaQuery.addEventListener) {
      mediaQuery.addEventListener('change', handleQueryChange);
      return () => mediaQuery.removeEventListener('change', handleQueryChange);
    } else {
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      const legacyQuery = mediaQuery as any;
      legacyQuery.addListener(handleQueryChange);
      return () => legacyQuery.removeListener(handleQueryChange);
    }
  }, [breakpoint]);

  return isMobile;
}

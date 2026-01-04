// Root layout with providers
import { Inter } from 'next/font/google';
import './globals.css';
import { Providers } from './providers';

const inter = Inter({ 
  subsets: ['latin'],
  display: 'swap',
  fallback: ['system-ui', 'arial'],
  variable: '--font-inter',
});

export const metadata = {
  title: 'AIPatterner - Reminder Management',
  description: 'Self-hosted reminder and pattern learning system',
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en">
      <body className={`${inter.variable} ${inter.className}`}>
        <Providers>{children}</Providers>
      </body>
    </html>
  );
}


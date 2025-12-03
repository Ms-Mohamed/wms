import { ReactNode, memo } from 'react';
import { Box, Flex } from '@chakra-ui/react';
import Sidebar from './Sidebar';
import Header from './Header';

interface LayoutProps {
  children: ReactNode;
}

const Layout = memo(({ children }: LayoutProps) => {
  return (
    <Flex h="100vh" overflow="hidden">
      <Sidebar />
      <Flex direction="column" flex="1" overflow="hidden">
        <Header />
        <Box flex="1" overflowY="auto" bg="gray.50" p={6}>
          {children}
        </Box>
      </Flex>
    </Flex>
  );
});

Layout.displayName = 'Layout';

export default Layout;


import { memo } from 'react';
import { Outlet } from 'react-router-dom';
import { Box, Flex } from '@chakra-ui/react';
import Sidebar from './Sidebar';
import Header from './Header';

const Layout = memo(() => (
  <Flex h="100vh" overflow="hidden">
    <Sidebar />
    <Flex direction="column" flex="1" minW={0} overflow="hidden">
      <Header />
      <Box as="main" flex="1" overflowY="auto" px={{ base: 4, md: 8 }} py={6}>
        <Box maxW="1280px" mx="auto"><Outlet /></Box>
      </Box>
    </Flex>
  </Flex>
));

Layout.displayName = 'Layout';
export default Layout;

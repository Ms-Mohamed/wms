import { memo, useMemo } from 'react';
import { Box, Flex, Text, VStack } from '@chakra-ui/react';
import { useTranslation } from 'react-i18next';
import { NavLink } from 'react-router-dom';
import {
  Squares2X2Icon, ShoppingCartIcon, CubeIcon, ChartBarIcon, ArchiveBoxIcon,
  TruckIcon, ClipboardDocumentListIcon, MapPinIcon, ArrowUturnLeftIcon,
} from '@heroicons/react/24/outline';

interface NavItem { path: string; label: string; icon: typeof Squares2X2Icon }
interface NavGroup { title: string; items: NavItem[] }

export const SIDEBAR_WIDTH = '248px';

const Sidebar = memo(() => {
  const { t } = useTranslation();

  const groups: NavGroup[] = useMemo(() => [
    { title: t('nav.groups.overview'), items: [
      { path: '/dashboard', label: t('nav.dashboard'), icon: Squares2X2Icon },
      { path: '/analytics', label: t('nav.analytics'), icon: ChartBarIcon },
    ] },
    { title: t('nav.groups.sales'), items: [
      { path: '/orders', label: t('nav.orders'), icon: ShoppingCartIcon },
      { path: '/returns', label: t('nav.returns'), icon: ArrowUturnLeftIcon },
    ] },
    { title: t('nav.groups.supply'), items: [
      { path: '/purchase-orders', label: t('nav.purchases'), icon: ClipboardDocumentListIcon },
      { path: '/suppliers', label: t('nav.suppliers'), icon: TruckIcon },
    ] },
    { title: t('nav.groups.inventory'), items: [
      { path: '/products', label: t('nav.products'), icon: CubeIcon },
      { path: '/stock', label: t('nav.stock'), icon: ArchiveBoxIcon },
      { path: '/locations', label: t('nav.locations'), icon: MapPinIcon },
    ] },
  ], [t]);

  return (
    <Box as="nav" aria-label="Main" w={SIDEBAR_WIDTH} flexShrink={0} h="100vh" bg="ink.900" color="ink.200" px={3} py={5} overflowY="auto" display={{ base: 'none', md: 'block' }}>
      <Flex align="center" gap={2.5} px={3} mb={7}>
        <Flex w={8} h={8} borderRadius="md" bg="brand.500" color="white" align="center" justify="center" fontWeight={800} fontSize="sm">W</Flex>
        <Text fontWeight={700} color="white" letterSpacing="-0.01em">WMS</Text>
      </Flex>
      <VStack align="stretch" spacing={5}>
        {groups.map((g) => (
          <Box key={g.title}>
            <Text px={3} mb={1.5} fontSize="11px" fontWeight={600} letterSpacing="0.08em" textTransform="uppercase" color="ink.400">{g.title}</Text>
            <VStack align="stretch" spacing={0.5}>
              {g.items.map((item) => (
                <NavLink key={item.path} to={item.path} style={{ textDecoration: 'none' }}>
                  {({ isActive }) => (
                    <Flex
                      align="center" gap={3} px={3} py={2} borderRadius="md" fontSize="sm"
                      fontWeight={isActive ? 600 : 500}
                      bg={isActive ? 'whiteAlpha.200' : 'transparent'}
                      color={isActive ? 'white' : 'ink.300'}
                      _hover={{ bg: isActive ? 'whiteAlpha.200' : 'whiteAlpha.100', color: 'white' }}
                      transition="background .12s"
                    >
                      <item.icon width={18} height={18} />
                      {item.label}
                    </Flex>
                  )}
                </NavLink>
              ))}
            </VStack>
          </Box>
        ))}
      </VStack>
    </Box>
  );
});

Sidebar.displayName = 'Sidebar';
export default Sidebar;
